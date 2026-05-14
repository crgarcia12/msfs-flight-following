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
        mcdu: null,
        mcduSide: 'left'
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
            // Local-state increment. We then POST the absolute value via /set.
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
        // Convert FBW MCDU tagged text into sanitized HTML. Mirrors desktop main.js.
        renderMcdu(s) {
            if (!s) return '';
            const classes = {
                '{white}':'mc-w','{cyan}':'mc-c','{green}':'mc-g',
                '{amber}':'mc-a','{magenta}':'mc-m',
                '{small}':'mc-sm','{big}':'mc-bg',
                '{right}':'mc-rt','{left}':'mc-lt'
            };
            const tokens = s.split(/(\{[^}]+\})/);
            let html = '', open = 0;
            for (const t of tokens) {
                if (!t) continue;
                if (t === '{end}') { if (open > 0) { html += '</span>'; open--; } }
                else if (t === '{sp}') { html += ' '; }
                else if (t.startsWith('{') && t.endsWith('}')) {
                    const cls = classes[t];
                    if (cls) { html += '<span class="' + cls + '">'; open++; }
                } else {
                    html += t.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
                }
            }
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
        },
        updateMap() {
            if (!this.map || !this.marker || !this.ac.latitude) return;
            const pos = [this.ac.latitude, this.ac.longitude];
            this.marker.setLatLng(pos);
            this.marker.setRotationAngle(this.ac.trueHeading || 0);
            this.map.setView(pos, this.map.getZoom(), { animate: false });
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
        });

        try {
            await ws.start();
            this.connected = true;
        } catch (e) {
            console.warn('hub start failed', e);
        }
    }
});
