'use strict';

/**
 * Mobile single-page app for MSFS Flight Following.
 * Three tabs: Flight (telemetry + FMA), Map (mini Leaflet), FCU (full autopilot).
 * Backend contract is identical to the desktop view:
 *   - SignalR hub /ws emits "ReceiveData" with { isConnected, data, services }
 *   - REST POSTs to /api/fcu/* and /api/sim/recover-alt
 */

const app = new Vue({
    el: "#app",
    data: {
        ac: {},
        connected: false,
        srvSim: false,
        simBridgeStatus: 'Disabled',
        simWriteEnabled: false,
        tab: 'flight',
        map: null,
        marker: null,
        fcu: { spd: 250, hdg: 0, alt: 10000, vs: 0, altStep: 1000 },
        // Per-knob timestamp of the last user touch. Used by syncFcuFromCockpit()
        // to skip overwriting a value the user just dialed.
        fcuTouched: { spd: 0, hdg: 0, alt: 0, vs: 0 },
        mcdu: null,
        mcduSide: 'left',
        // VATSIM controlled-area overlay — only the bits that the template reads
        // live here. The heavy Leaflet/GeoJSON state is parked on `this` in
        // created() so Vue 2's reactivity doesn't try to walk Maps & DOM refs.
        vatsim: { controllers: [], insideFir: null }
    },
    created() {
        // Non-reactive overlay state (Leaflet layers, native Maps, WeakMap, etc.)
        this.firFeaturesById = null;
        this.firFeaturesBboxes = null;
        this.firLoadState = 'idle';
        this.firLoadPromise = null;
        this.activeSectors = new Map();
        this.firCanvasRenderer = null;
        this.vatsimSectorsLayer = null;
        this.insideKey = null;
    },
    computed: {
        connClass() {
            if (!this.connected) return 'red';
            if (!this.srvSim) return 'amber';
            return 'green';
        },
        connText() {
            if (!this.connected) return 'OFFLINE';
            if (!this.srvSim) return 'NO SIM';
            return 'CONNECTED';
        },
        phase() { return this.ac.flightPhase || 'Preflight'; },
        mcduOfflineHint() {
            const s = this.simBridgeStatus;
            if (s === 'Streaming')         return 'Reconnecting...';
            if (s === 'AwaitingAircraft')  return 'SimBridge is up. In the cockpit MCDU, open MCDU MENU → AIRCRAFT OPTIONS and turn ON "SimBridge" (CONFIG_SIMBRIDGE_ENABLED).';
            if (s === 'Connecting')        return 'Reaching SimBridge at ws://localhost:8380...';
            return 'Set Features.SimBridge.Enabled = true and restart.';
        },
        hasFma() {
            return !!(this.ac.a32nxFmaVerticalMode || this.ac.a32nxFmaLateralMode || this.ac.a32nxAutothrustMode);
        },
        altDisplay() {
            // Show "FL350" above ~18000 ft to mirror the Airbus PFD altitude window
            // (INDICATED ALTITUDE already respects the baro setting).
            const alt = (this.ac && this.ac.altitude) || 0;
            if (alt >= 18000) {
                const fl = Math.round(alt / 100);
                return { value: 'FL' + fl.toString().padStart(3, '0'), unit: '' };
            }
            return { value: this.fmt(alt, 0), unit: 'ft' };
        },
        srsActive() { return !!this.ac.fmaIsSrs; },
        apSummaryIcon() {
            const v = this.ac.fmaPitch;
            if (v === 'CLB' || v === 'OP CLB' || v === 'SRS' || v === 'SRS GA') return 'flight_takeoff';
            if (v === 'DES' || v === 'OP DES' || v === 'LAND' || v === 'FLARE' || v === 'G/S' || v === 'G/S*') return 'flight_land';
            if (v === 'ALT' || v === 'ALT*' || v === 'ALT CST') return 'horizontal_rule';
            const ap = this.ac.autopilot;
            if (ap && ap.master) return 'auto_mode';
            return 'pan_tool';
        },
        apSummary() {
            const a = this.ac;
            if (a.fmaPitch || a.fmaRoll) {
                const parts = [];
                const v = a.fmaPitch;
                if (v === 'CLB') parts.push('CLIMB');
                else if (v === 'OP CLB') parts.push('OPEN CLIMB');
                else if (v === 'DES') parts.push('DESCENT');
                else if (v === 'OP DES') parts.push('OPEN DESCENT');
                else if (v === 'ALT') parts.push('ALT HOLD');
                else if (v === 'ALT*') parts.push('CAPTURING ALT');
                else if (v === 'ALT CST') parts.push('AT CONSTRAINT');
                else if (v === 'V/S') parts.push('V/S');
                else if (v === 'FPA') parts.push('FPA');
                else if (v === 'SRS') parts.push('TAKEOFF');
                else if (v === 'SRS GA') parts.push('GO-AROUND');
                else if (v === 'G/S' || v === 'G/S*') parts.push('GLIDESLOPE');
                else if (v === 'LAND') parts.push('LANDING');
                else if (v === 'FLARE') parts.push('FLARE');
                else if (v) parts.push(v);
                const l = a.fmaRoll;
                if (l === 'NAV') parts.push('on route');
                else if (l === 'HDG') parts.push('HDG');
                else if (l === 'TRK') parts.push('TRK');
                else if (l === 'LOC' || l === 'LOC*') parts.push('on LOC');
                else if (l === 'RWY') parts.push('RWY');
                else if (l) parts.push(l.toLowerCase());
                return parts.length ? parts.join(', ').toUpperCase() : 'AUTOFLIGHT';
            }
            const ap = a.autopilot || {};
            if (!ap.master && !ap.flightDirector) return 'HAND FLYING';
            const bits = [];
            if (ap.altitude || ap.verticalHold) bits.push('ALT HOLD');
            if (ap.heading) bits.push('HDG');
            if (ap.nav1) bits.push('NAV');
            if (ap.approach) bits.push('APPR');
            return bits.length ? bits.join(', ') : (ap.master ? 'AP ENGAGED' : 'FD ONLY');
        },
        fuelPct() {
            if (!this.ac.totalFuel) return 0;
            return Math.round((this.ac.currentFuel || 0) / this.ac.totalFuel * 100);
        },
        wind() {
            const dir = Math.round(((this.ac.windDirectionDegrees || 0) % 360 + 360) % 360);
            const kts = Math.round(this.ac.windVelocityKnots || 0);
            return `${String(dir).padStart(3,'0')}° / ${kts} kt`;
        },
        oat() {
            const c = this.ac.outsideAirTempC;
            return (typeof c === 'number') ? `${Math.round(c)} °C` : '—';
        }
    },
    methods: {
        fmt(v, digits) {
            if (typeof v !== 'number' || isNaN(v)) return '—';
            return v.toFixed(digits);
        },
        async post(path, body) {
            // Client-side READ-ONLY gate. Mirrors desktop main.js.
            if (!this.simWriteEnabled) {
                console.warn('FCU write blocked: app is in READ-ONLY mode (Features.Sim.WriteEnabled=false).');
                return;
            }
            // PULL means "fly the dialed value" → user owns this knob.
            // PUSH means "go back to managed (FMGC) value" → resume cockpit sync.
            const m = /^(spd|hdg|alt|vs)\/(pull|push)$/.exec(path);
            if (m) {
                if (m[2] === 'pull') this.fcuTouched[m[1]] = Date.now();
                else                 this.fcuTouched[m[1]] = 0;
            }
            try {
                const opts = { method: 'POST', headers: { 'Content-Type': 'application/json' } };
                if (body !== undefined) opts.body = JSON.stringify(body);
                await fetch('/api/fcu/' + path, opts);
            } catch (e) { console.warn('fcu post failed', e); }
        },
        step(knob, delta) {
            if (!this.simWriteEnabled) {
                console.warn('FCU step blocked: app is in READ-ONLY mode.');
                return;
            }
            // Mark this knob as user-touched so syncFcuFromCockpit() won't
            // overwrite the dialed value on the next data tick.
            this.fcuTouched[knob] = Date.now();
            if (knob === 'spd') {
                this.fcu.spd = Math.max(100, Math.min(399, this.fcu.spd + delta));
                this.post('spd/set', { value: this.fcu.spd });
            } else if (knob === 'hdg') {
                this.fcu.hdg = (this.fcu.hdg + delta + 360) % 360;
                this.post('hdg/set', { value: this.fcu.hdg });
            } else if (knob === 'alt') {
                this.fcu.alt = Math.max(100, Math.min(49000, this.fcu.alt + delta));
                this.post('alt/set', { value: this.fcu.alt });
            } else if (knob === 'vs') {
                this.fcu.vs = Math.max(-6000, Math.min(6000, this.fcu.vs + delta));
                this.post('vs/set', { value: this.fcu.vs });
            }
        },
        /// Copies the live cockpit FCU windows into the panel's editable values,
        /// so PUSH / PULL / ±1 / ±10 start from whatever the cockpit shows
        /// instead of the hardcoded defaults. Skips knobs the user has touched
        /// in the last 1.5 s so a click-then-tick race doesn't reset the dial.
        syncFcuFromCockpit() {
            const d = this.ac && this.ac.fcuDisplay;
            if (!d || d.source === 'none') return;
            const now = Date.now();
            const fresh = (key) => (now - (this.fcuTouched[key] || 0)) > 1500;
            // SPD: skip if mach mode (.82) or dashes (---).
            if (fresh('spd') && !d.spdIsMach && /^\d+$/.test(d.spdText)) {
                this.fcu.spd = parseInt(d.spdText, 10);
            }
            if (fresh('hdg') && /^\d+$/.test(d.hdgText)) {
                // Cockpit shows "360" for north — keep it for display parity, but
                // PULL/SET expects 0-359 range so wrap 360 → 0 for the dial value.
                const h = parseInt(d.hdgText, 10);
                this.fcu.hdg = h === 360 ? 0 : h;
            }
            if (fresh('alt') && /^\d+$/.test(d.altText)) {
                this.fcu.alt = parseInt(d.altText, 10);
            }
            if (fresh('vs') && d.vsActive && !d.vsIsFpa && /^[+-]?\d+/.test(d.vsText)) {
                this.fcu.vs = parseInt(d.vsText, 10);
            }
        },
        async recoverAltitude() {
            if (!this.simWriteEnabled) {
                console.warn('Recover-altitude blocked: READ-ONLY mode.');
                return;
            }
            try {
                await fetch('/api/sim/recover-alt', { method: 'POST' });
            } catch (e) { console.warn('recover alt failed', e); }
        },
        async beginDescent() {
            if (!this.simWriteEnabled) {
                console.warn('Begin-descent blocked: READ-ONLY mode.');
                return;
            }
            try {
                await fetch('/api/sim/begin-descent', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ value: this.fcu.alt })
                });
            } catch (e) { console.warn('begin-descent failed', e); }
        },
        async syncQnh() {
            // B-key equivalent — fires BAROMETRIC event in MSFS.
            if (!this.simWriteEnabled) {
                console.warn('QNH sync blocked: READ-ONLY mode.');
                return;
            }
            try {
                await fetch('/api/sim/qnh-sync', { method: 'POST' });
            } catch (e) { console.warn('qnh-sync failed', e); }
        },
        async mcduKey(key) {
            if (!this.simWriteEnabled) {
                console.warn('MCDU input blocked: app is in READ-ONLY mode.');
                return;
            }
            try {
                await fetch('/api/mcdu/key', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ side: this.mcduSide, key })
                });
            } catch (e) {
                console.warn('mcdu key failed', e);
            }
        },
        // Convert FBW MCDU tagged text into sanitized HTML. Mirrors desktop main.js.
        renderMcdu(s) {
            if (!s) return '';
            const classes = {
                'white':'mc-w','cyan':'mc-c','green':'mc-g',
                'amber':'mc-a','magenta':'mc-m','red':'mc-r','yellow':'mc-y','inop':'mc-inop',
                'small':'mc-sm','big':'mc-bg',
                'right':'mc-rt','left':'mc-lt','center':'mc-ct'
            };
            // Closed set of recognised tags. Anything else (e.g. "{ILS11-Y") is
            // FBW's left-chevron font glyph used as a line-select indicator and
            // must be preserved as literal text — see issue where approach names
            // disappeared on the ARRIVAL/APPR page.
            const tagRe = /\{(?:white|cyan|green|amber|magenta|red|yellow|inop|small|big|right|left|center|end|sp)\}/g;
            let html = '', open = 0, lastIdx = 0, m;
            const emitText = (txt) => {
                if (!txt) return;
                // FBW uses '{' / '}' as left/right chevron font glyphs at LSK
                // boundaries. Map them to ASCII chevrons for visibility.
                html += txt
                    .replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')
                    .replace(/\{/g,'<').replace(/\}/g,'>');
            };
            while ((m = tagRe.exec(s)) !== null) {
                if (m.index > lastIdx) emitText(s.slice(lastIdx, m.index));
                const tag = m[0];
                if (tag === '{end}') { if (open > 0) { html += '</span>'; open--; } }
                else if (tag === '{sp}') { html += ' '; }
                else {
                    const cls = classes[tag.slice(1, -1)];
                    if (cls) { html += '<span class="' + cls + '">'; open++; }
                }
                lastIdx = tagRe.lastIndex;
            }
            if (lastIdx < s.length) emitText(s.slice(lastIdx));
            while (open > 0) { html += '</span>'; open--; }
            return html;
        },
        setupMap() {
            if (this.map) {
                this.map.invalidateSize();
                return;
            }
            const center = [
                (this.ac && this.ac.latitude) || 47.5,
                (this.ac && this.ac.longitude) || 10.0
            ];
            this.map = L.map('map', { zoomControl: true, attributionControl: false }).setView(center, 6);
            L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
                subdomains: 'abcd', maxZoom: 18
            }).addTo(this.map);

            const icon = L.icon({
                iconUrl: '/assets/airplane.svg',
                iconSize: [40, 40], iconAnchor: [20, 20]
            });
            this.marker = L.marker(center, { icon, rotationAngle: 0, rotationOrigin: 'center center' }).addTo(this.map);

            // VATSIM sectors overlay (canvas-rendered so oceanic FIRs don't choke
            // the SVG pane). Identical behaviour to the desktop overlay.
            this.firCanvasRenderer = L.canvas({ padding: 0.5 });
            this.vatsimSectorsLayer = L.layerGroup().addTo(this.map);

            // If VATSIM data already arrived before the map was set up, fold it in.
            if (this.vatsim.controllers.length > 0) {
                this.kickFirOverlay(this.vatsim.controllers);
            }
        },
        updateMap() {
            if (!this.map || !this.marker || !this.ac.latitude) return;
            const pos = [this.ac.latitude, this.ac.longitude];
            this.marker.setLatLng(pos);
            this.marker.setRotationAngle(this.ac.trueHeading || 0);
            this.map.setView(pos, this.map.getZoom(), { animate: false });
            this.recomputeInsideFir();
        },
        // ---------- VATSIM FIR overlay -------------------------------------------------
        kickFirOverlay(controllers) {
            if (!this.vatsimSectorsLayer) return;
            if (this.firLoadState === 'ready') {
                this.rebuildVatsimSectors(controllers);
            } else if (this.firLoadState === 'idle') {
                setTimeout(() => {
                    this.loadFirBoundaries().then(ok => {
                        if (ok) this.rebuildVatsimSectors(this.vatsim.controllers || []);
                    });
                }, 1500);
            } else if (this.firLoadState === 'loading' && this.firLoadPromise) {
                this.firLoadPromise.then(ok => {
                    if (ok) this.rebuildVatsimSectors(this.vatsim.controllers || []);
                });
            }
        },
        async loadFirBoundaries() {
            if (this.firLoadState === 'ready')  return true;
            if (this.firLoadState === 'failed') return false;
            if (this.firLoadPromise)            return this.firLoadPromise;
            this.firLoadState = 'loading';
            this.firLoadPromise = (async () => {
                try {
                    const r = await fetch('https://cdn.jsdelivr.net/gh/vatsimnetwork/vatspy-data-project@master/Boundaries.geojson');
                    if (!r.ok) throw new Error('HTTP ' + r.status);
                    const fc = await r.json();
                    const byId   = new Map();
                    const bboxes = new WeakMap();
                    for (const f of (fc.features || [])) {
                        const id = f.properties && f.properties.id;
                        if (!id) continue;
                        if (!byId.has(id)) byId.set(id, []);
                        byId.get(id).push(f);
                        bboxes.set(f, this.computeBbox(f.geometry));
                    }
                    this.firFeaturesById   = byId;
                    this.firFeaturesBboxes = bboxes;
                    this.firLoadState = 'ready';
                    return true;
                } catch (e) {
                    console.warn('FIR boundaries load failed', e);
                    this.firLoadState = 'failed';
                    return false;
                }
            })();
            return this.firLoadPromise;
        },
        computeBbox(geom) {
            let minLat = 90, maxLat = -90, minLon = 180, maxLon = -180;
            const walk = (ring) => {
                for (const c of ring) {
                    const lon = c[0], lat = c[1];
                    if (lat < minLat) minLat = lat;
                    if (lat > maxLat) maxLat = lat;
                    if (lon < minLon) minLon = lon;
                    if (lon > maxLon) maxLon = lon;
                }
            };
            if (!geom) return { minLat, maxLat, minLon, maxLon };
            if (geom.type === 'Polygon') {
                for (const ring of (geom.coordinates || [])) walk(ring);
            } else if (geom.type === 'MultiPolygon') {
                for (const poly of (geom.coordinates || []))
                    for (const ring of poly) walk(ring);
            }
            return { minLat, maxLat, minLon, maxLon };
        },
        rebuildVatsimSectors(controllers) {
            if (!this.firFeaturesById || !this.vatsimSectorsLayer) return;
            const wanted = new Map();
            for (const c of (controllers || [])) {
                const facility = c.facilityShort;
                if (facility !== 'CTR' && facility !== 'FSS') continue;
                const segs = (c.callsign || '').split('_');
                if (segs.length < 2) continue;
                const icao = segs[0];
                const feats = this.firFeaturesById.get(icao);
                if (!feats) continue;
                const key = c.callsign + '|' + icao;
                wanted.set(key, { icao, feats, controller: c, isCtr: facility === 'CTR' });
            }
            for (const [key, entry] of Array.from(this.activeSectors.entries())) {
                if (!wanted.has(key)) {
                    this.vatsimSectorsLayer.removeLayer(entry.layerGroup);
                    this.activeSectors.delete(key);
                }
            }
            for (const [key, m] of wanted) {
                if (this.activeSectors.has(key)) continue;
                const baseColor = m.isCtr ? '#22d3ee' : '#fbbf24';
                const layerGroup = L.featureGroup();
                for (const feat of m.feats) {
                    const layer = L.geoJSON(feat, {
                        renderer: this.firCanvasRenderer,
                        style: { color: baseColor, weight: 1.5, opacity: 0.85, fillColor: baseColor, fillOpacity: 0.04 },
                        interactive: false
                    });
                    layer.addTo(layerGroup);
                }
                layerGroup.addTo(this.vatsimSectorsLayer);
                this.activeSectors.set(key, {
                    controller: m.controller,
                    feats: m.feats,
                    layerGroup,
                    baseColor,
                    icao: m.icao,
                    inside: false
                });
            }
            this.recomputeInsideFir();
        },
        pointInRing(lon, lat, ring) {
            let inside = false;
            for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
                const xi = ring[i][0], yi = ring[i][1];
                const xj = ring[j][0], yj = ring[j][1];
                const intersect = ((yi > lat) !== (yj > lat)) && (lon < (xj - xi) * (lat - yi) / (yj - yi) + xi);
                if (intersect) inside = !inside;
            }
            return inside;
        },
        pointInGeometry(lon, lat, geom) {
            if (!geom) return false;
            if (geom.type === 'Polygon') {
                const rings = geom.coordinates;
                if (!rings || rings.length === 0) return false;
                if (!this.pointInRing(lon, lat, rings[0])) return false;
                for (let i = 1; i < rings.length; i++) {
                    if (this.pointInRing(lon, lat, rings[i])) return false;
                }
                return true;
            }
            if (geom.type === 'MultiPolygon') {
                for (const poly of (geom.coordinates || [])) {
                    if (!poly || poly.length === 0) continue;
                    if (!this.pointInRing(lon, lat, poly[0])) continue;
                    let inHole = false;
                    for (let i = 1; i < poly.length; i++) {
                        if (this.pointInRing(lon, lat, poly[i])) { inHole = true; break; }
                    }
                    if (!inHole) return true;
                }
                return false;
            }
            return false;
        },
        recomputeInsideFir() {
            const lat = this.ac && this.ac.latitude;
            const lon = this.ac && this.ac.longitude;
            if (lat == null || lon == null) return;
            if (this.activeSectors.size === 0 && this.insideKey == null) return;
            let foundKey = null, foundEntry = null;
            for (const [key, entry] of this.activeSectors) {
                let inside = false;
                for (const feat of entry.feats) {
                    const bbox = this.firFeaturesBboxes && this.firFeaturesBboxes.get(feat);
                    if (bbox) {
                        if (lat < bbox.minLat || lat > bbox.maxLat || lon < bbox.minLon || lon > bbox.maxLon) continue;
                    }
                    if (this.pointInGeometry(lon, lat, feat.geometry)) { inside = true; break; }
                }
                if (inside !== entry.inside) {
                    const color = inside ? '#ef4444' : entry.baseColor;
                    const weight = inside ? 3 : 1.5;
                    const fillOpacity = inside ? 0.15 : 0.04;
                    entry.layerGroup.eachLayer(lyr => {
                        if (lyr.setStyle) lyr.setStyle({ color, weight, fillColor: color, fillOpacity });
                    });
                    entry.inside = inside;
                }
                if (inside && foundKey == null) { foundKey = key; foundEntry = entry; }
            }
            if (foundKey !== this.insideKey) {
                this.insideKey = foundKey;
                this.vatsim.insideFir = foundEntry ? {
                    icao: foundEntry.icao,
                    callsign: foundEntry.controller.callsign,
                    controllerName: foundEntry.controller.controllerName,
                    frequency: foundEntry.controller.frequency,
                    facility: foundEntry.controller.facilityShort
                } : null;
            }
        }
    },
    watch: {
        ac() { if (this.tab === 'map') this.updateMap(); }
    },
    async mounted() {
        const ws = new signalR.HubConnectionBuilder()
            .withUrl("/ws")
            .withAutomaticReconnect()
            .build();

        ws.onreconnecting(() => { this.connected = false; });
        ws.onreconnected(() => { this.connected = true; });
        ws.onclose(() => { this.connected = false; });

        ws.on("ReceiveData", (data) => {
            this.connected = true;
            this.srvSim = !!(data.services && data.services.sim);
            this.simBridgeStatus = (data.services && data.services.simBridgeStatus) || 'Disabled';
            this.simWriteEnabled = !!(data.services && data.services.simWriteEnabled);
            if (data.data) this.ac = data.data;
            this.mcdu = data.mcdu || null;
            // Keep the FCU panel's editable values in sync with the cockpit
            // unless the user has actively touched a knob.
            this.syncFcuFromCockpit();
        });

        ws.on("ReceiveVatsim", (data) => {
            this.vatsim.controllers = (data && data.controllers) ? data.controllers : [];
            // The map tab may not have been opened yet — kickFirOverlay no-ops in
            // that case; setupMap() will fold in the latest data when it runs.
            this.kickFirOverlay(this.vatsim.controllers);
        });

        try {
            await ws.start();
            this.connected = true;
        } catch (e) {
            console.warn('hub start failed', e);
        }
    }
});
