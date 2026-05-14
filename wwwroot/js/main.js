'use strict';

class FlightMap {
    constructor() {
        this.map = null;
        this.marker = null;
        this.followButton = null;
        this.flightPathButton = null;
        this.trailButton = null;
        this.flightPath = null;
        this.layerControl = null;
        this.trail = null;
        this.trailPoints = [];
        this.maxTrailPoints = 600;
        this.trailVisible = true;
        this.vatsimMarkerLayer = null;
        this.vatsimCoverageLayer = null;
        this.vatsimMarkersByCallsign = new Map();
        this.InitMap();
    }

    UpdatePosition(lat, lng, heading) {
        const newPos = new L.LatLng(lat, lng);

        this.marker.setLatLng(newPos);
        this.marker.setRotationAngle(heading);

        // Append to trail
        const last = this.trailPoints[this.trailPoints.length - 1];
        if (!last || last.lat !== lat || last.lng !== lng) {
            this.trailPoints.push({ lat, lng });
            if (this.trailPoints.length > this.maxTrailPoints) this.trailPoints.shift();
            if (this.trail && this.trailVisible) {
                this.trail.setLatLngs(this.trailPoints.map(p => [p.lat, p.lng]));
            }
        }

        if (this.followButton && !this.followButton.button.classList.contains("disabled"))
            this.map.setView(newPos);
    }

    DrawFlightPath(from, to) {
        if (this.flightPathButton && this.flightPathButton.button.classList.contains("disabled"))
            return;
        if (this.flightPath) {
            this.flightPath.remove();
        }
        this.flightPath = L.Polyline.Arc(from, to, {
            vertices: 200,
            color: "#22d3ee",
            weight: 2,
            opacity: 0.8,
            dashArray: "6,4"
        }).addTo(this.map);
    }

    ToggleTrail() {
        this.trailVisible = !this.trailVisible;
        if (this.trailVisible) {
            this.trail.setLatLngs(this.trailPoints.map(p => [p.lat, p.lng]));
            this.trail.addTo(this.map);
        } else {
            this.trail.remove();
        }
    }

    ClearTrail() {
        this.trailPoints = [];
        if (this.trail) this.trail.setLatLngs([]);
    }

    async InitMap() {
        const cartoDBlayer = L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>',
            subdomains: 'abcd',
            maxZoom: 19
        });

        const cartoDark = L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>',
            subdomains: 'abcd',
            maxZoom: 19
        });

        const openStreetMap = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        });

        const topoMap = L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {
            maxZoom: 17,
            attribution: 'Map data: &copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors, <a href="http://viewfinderpanoramas.org">SRTM</a> | Map style: &copy; <a href="https://opentopomap.org">OpenTopoMap</a>'
        });

        const navAidsLayer = new L.TileLayer("https://{s}.tile.maps.openaip.net/geowebcache/service/tms/1.0.0/openaip_basemap@EPSG%3A900913@png/{z}/{x}/{y}.png", {
            maxZoom: 14,
            minZoom: 4,
            tms: true,
            subdomains: "12",
            format: "image/png",
            transparent: true,
            attribution: "<a href=\"https://www.openaip.net\" target=\"_blank\">openAIP</a>"
        });

        const baseMaps = {
            "Dark (Carto)": cartoDark,
            "Voyager (Carto)": cartoDBlayer,
            "OpenStreetMap": openStreetMap,
            "Topography": topoMap
        };

        this.map = L.map('map', {
            center: [0, 0],
            zoom: 4,
            layers: [cartoDark],
            preferCanvas: true,
            zoomControl: false
        });

        L.control.zoom({ position: 'topleft' }).addTo(this.map);

        this.map.on("dragstart", () => { if (this.followButton) this.followButton.disable(); });

        const icon = L.icon({
            iconUrl: "./assets/airplane.svg",
            iconSize: [40, 40]
        });

        this.marker = L.marker([0, 0], {
            icon: icon,
            rotationAngle: 0,
            rotationOrigin: 'center center'
        }).addTo(this.map);

        // Trail polyline
        this.trail = L.polyline([], { color: "#22d3ee", weight: 3, opacity: 0.7 }).addTo(this.map);

        this.followButton = L.easyButton('<span class="material-icons">flight</span>', function () {
            const state = this.button.classList.contains("disabled");
            if (state) this.enable(); else this.disable();
        }, 'Follow aircraft').addTo(this.map);

        this.flightPathButton = L.easyButton('<span class="material-icons">timeline</span>', () => {
            if (!this.flightPath) return;
            const state = this.flightPathButton.button.classList.contains("disabled");
            if (state) {
                this.flightPath.addTo(this.map);
                this.flightPathButton.enable();
            } else {
                this.flightPath.remove();
                this.flightPathButton.disable();
            }
        }, 'Display flight path').addTo(this.map);

        this.trailButton = L.easyButton('<span class="material-icons">route</span>', () => {
            this.ToggleTrail();
        }, 'Toggle breadcrumb trail').addTo(this.map);

        const overlayMaps = {
            "OpenAIP NavAids": navAidsLayer,
            "VATSIM Stations": this.vatsimMarkerLayer = L.layerGroup().addTo(this.map),
            "VATSIM Coverage": this.vatsimCoverageLayer = L.layerGroup()
        };
        this.layerControl = L.control.layers(baseMaps, overlayMaps).addTo(this.map);
    }

    /**
     * Replace all VATSIM station markers + coverage circles on the map.
     * Called from the Vue watcher whenever ReceiveVatsim updates controllers.
     */
    UpdateVatsim(controllers) {
        if (!this.map || !this.vatsimMarkerLayer) return;

        this.vatsimMarkerLayer.clearLayers();
        this.vatsimCoverageLayer.clearLayers();
        this.vatsimMarkersByCallsign.clear();

        if (!Array.isArray(controllers) || controllers.length === 0) return;

        const facilityColors = {
            DEL:  '#94a3b8',
            GND:  '#a78bfa',
            TWR:  '#f87171',
            APP:  '#fb923c',
            DEP:  '#fb923c',
            CTR:  '#60a5fa',
            FSS:  '#fbbf24',
            ATIS: '#22d3ee',
            OBS:  '#6b7280'
        };
        // Facilities whose coverage circle is worth drawing (others are tiny / cluttered).
        const drawCoverageFor = new Set(['CTR', 'APP', 'DEP', 'FSS']);

        for (const c of controllers) {
            if (typeof c.latitudeDeg !== 'number' || typeof c.longitudeDeg !== 'number') continue;
            if (!isFinite(c.latitudeDeg) || !isFinite(c.longitudeDeg)) continue;
            // VATSIM publishes (0,0) for positions it can't resolve — skip those.
            if (c.latitudeDeg === 0 && c.longitudeDeg === 0) continue;

            const facility = c.facilityShort || 'OBS';
            const color = facilityColors[facility] || '#34d399';
            const atis = c.atisCode ? `<span class="vatsim-marker-atis">${c.atisCode}</span>` : '';
            const html = `<div class="vatsim-marker-badge" style="background:${color}">`
                + `<span class="vatsim-marker-facility">${facility}</span>`
                + `<span class="vatsim-marker-callsign">${c.callsign}</span>`
                + `${atis}</div>`;

            const icon = L.divIcon({
                className: 'vatsim-marker',
                html,
                iconSize: null,
                iconAnchor: [0, 0]
            });

            const marker = L.marker([c.latitudeDeg, c.longitudeDeg], { icon, riseOnHover: true });
            const popup = `<div class="vatsim-popup">`
                + `<div class="vatsim-popup-head" style="border-color:${color}">`
                + `<strong>${c.callsign}</strong> <span class="vatsim-popup-facility">${facility}</span></div>`
                + `<div class="vatsim-popup-freq">${c.frequency || '---'}</div>`
                + `<div class="vatsim-popup-name">${c.controllerName || ''}</div>`
                + `<div class="vatsim-popup-meta">${(c.distanceNm ?? '?')} NM · range ${(c.visualRangeNm ?? '?')} NM</div>`
                + (c.atisText ? `<pre class="vatsim-popup-atis">${c.atisText}</pre>` : '')
                + `</div>`;
            marker.bindPopup(popup, { maxWidth: 360 });
            marker.addTo(this.vatsimMarkerLayer);
            this.vatsimMarkersByCallsign.set(c.callsign, marker);

            if (drawCoverageFor.has(facility) && c.visualRangeNm > 0) {
                // 1 NM = 1852 m
                L.circle([c.latitudeDeg, c.longitudeDeg], {
                    radius: c.visualRangeNm * 1852,
                    color,
                    weight: 1,
                    fillColor: color,
                    fillOpacity: 0.05,
                    opacity: 0.4,
                    interactive: false
                }).addTo(this.vatsimCoverageLayer);
            }
        }
    }
}

const AGENT_THEMES = {
    Pilot:     { icon: 'flight_takeoff', color: '#60a5fa' },
    Copilot:   { icon: 'person',         color: '#34d399' },
    Operator:  { icon: 'support_agent',  color: '#f87171' },
    Operations:{ icon: 'support_agent',  color: '#f87171' },
    Navigator: { icon: 'explore',        color: '#a78bfa' },
    Comms:     { icon: 'radio',          color: '#fbbf24' }
};

const app = new Vue({
    el: "#container",
    data: {
        map: null,
        acInfo: null,
        services: { sim: false, simBridge: false, eventHub: false, simBridgeStatus: 'Disabled', simWriteEnabled: false },
        simConnected: null,
        barHidden: false,
        showSearch: false,
        searchText: "",
        searchResults: [],
        searchTimeout: null,
        db: null,
        selectedAirport: null,
        showAlerts: false,
        alertForm: { property: "elapsed", operator: "equals", value: null, eteTimeHelper: null },
        agentevents: [],
        agentPanelOpen: true,
        unreadAgentEvents: 0,
        agentTtsEnabled: false,
        vatsim: {
            controllers: [],
            everReceived: false
        },
        vatsimPanelOpen: false,
        atisExpanded: {},
        speakingCallsign: null,
        fcuPanelOpen: false,
        fcu: {
            spd: 250,
            hdg: 0,
            alt: 10000,
            vs: 0,
            altStep: 1000
        },
        mcdu: null,
        mcduOpen: false,
        alerts: []
    },
    watch: {
        acInfo(newData, oldData) {
            if (!newData) return;
            this.map.UpdatePosition(newData.latitude, newData.longitude, newData.trueHeading);

            if (newData.gpsFlightPlanActive && (oldData === null || (newData.gpsWaypointIndex !== oldData.gpsWaypointIndex)))
                this.map.DrawFlightPath(
                    [newData.gpsNextWPLatitude, newData.gpsNextWPLongitude],
                    [newData.gpsPrevWPLatitude, newData.gpsPrevWPLongitude]
                );
        }
    },
    async mounted() {
        const wsConnection = new signalR.HubConnectionBuilder().withUrl("/ws").withAutomaticReconnect().build();
        wsConnection.start();

        wsConnection.on("ReceiveData", (data) => {
            this.simConnected = data.isConnected;
            this.acInfo = data.data;
            this.mcdu = data.mcdu || null;
            if (data.services) {
                this.services = {
                    sim: !!data.services.sim,
                    simBridge: !!data.services.simBridge,
                    eventHub: !!data.services.eventHub,
                    simBridgeStatus: data.services.simBridgeStatus || 'Disabled',
                    simWriteEnabled: !!data.services.simWriteEnabled
                };
            }
        });

        wsConnection.on("ReceiveAgentEvent", (data) => {
            this.agentevents.push(data);
            if (this.agentevents.length > 200) this.agentevents.shift();
            if (!this.agentPanelOpen) this.unreadAgentEvents++;
            if (this.agentTtsEnabled) this.speak(`${data.agent}: ${data.message}`);
            this.$nextTick(() => {
                const feed = this.$refs.agentFeed;
                if (feed) feed.scrollTop = feed.scrollHeight;
            });
        });

        wsConnection.on("ReceiveVatsim", (data) => {
            this.vatsim.everReceived = true;
            this.vatsim.controllers = (data && data.controllers) ? data.controllers : [];
            if (this.map && this.map.UpdateVatsim) {
                this.map.UpdateVatsim(this.vatsim.controllers);
            }
        });

        this.db = new Dexie("airports_database");
        this.db.version(1).stores({
            airports: '++id,country,name,icao,geolocation,radio,rwy,type'
        });

        const count = await this.db.airports.count();
        if (count === 0) {
            try {
                const response = await fetch("/get/airports");
                const result = await response.json();
                this.db.airports.bulkAdd(result.data);
            } catch (e) {
                console.warn("Could not load airports:", e);
            }
        }

        this.map = new FlightMap();

        L.easyButton('<span class="material-icons">notifications</span>', () => {
            this.showAlerts = !this.showAlerts;
        }, 'Alerts').addTo(this.map.map);

        const airportsLayer = L.layerGroup();
        this.db.airports.each(airport => {
            airportsLayer.addLayer(
                L.circleMarker([airport.geolocation.lat, airport.geolocation.lon], {
                    radius: 2,
                    color: "#22d3ee",
                    fillColor: "#22d3ee",
                    fillOpacity: 0.7
                }).on('click', () => { this.selectedAirport = airport; })
            );
        });
        this.map.layerControl.addOverlay(airportsLayer, "Airports");

        setTimeout(() => {
            if (this.simConnected === null) this.simConnected = false;
        }, 10000);
    },
    methods: {
        agentKey(agent) {
            return (agent || '').toLowerCase();
        },
        getAgentIcon(agent) {
            const t = AGENT_THEMES[agent];
            return t ? t.icon : 'smart_toy';
        },
        openAgentPanel() {
            this.agentPanelOpen = true;
            this.unreadAgentEvents = 0;
            this.$nextTick(() => {
                const feed = this.$refs.agentFeed;
                if (feed) feed.scrollTop = feed.scrollHeight;
            });
        },
        clearAgentEvents() {
            this.agentevents = [];
            this.unreadAgentEvents = 0;
        },
        toggleAtisExpand(callsign) {
            // Vue 2 needs Vue.set for new keys to be reactive.
            this.$set(this.atisExpanded, callsign, !this.atisExpanded[callsign]);
        },
        speak(text) {
            if (!text || !('speechSynthesis' in window)) return;
            try {
                const u = new SpeechSynthesisUtterance(text);
                u.rate = 1.05;
                u.pitch = 1.0;
                window.speechSynthesis.speak(u);
            } catch (e) { /* ignore */ }
        },
        speakAtis(ctrl) {
            if (!('speechSynthesis' in window)) return;
            if (this.speakingCallsign === ctrl.callsign) {
                window.speechSynthesis.cancel();
                this.speakingCallsign = null;
                return;
            }
            window.speechSynthesis.cancel();
            const text = `${ctrl.callsign} information ${ctrl.atisCode || ''}. ${ctrl.atisText || ''}`;
            const u = new SpeechSynthesisUtterance(text);
            u.rate = 1.0;
            u.onend = () => { if (this.speakingCallsign === ctrl.callsign) this.speakingCallsign = null; };
            u.onerror = () => { if (this.speakingCallsign === ctrl.callsign) this.speakingCallsign = null; };
            this.speakingCallsign = ctrl.callsign;
            window.speechSynthesis.speak(u);
        },
        async recoverAltitude() {
            if (!this.services.simWriteEnabled) {
                console.warn('Recover-altitude blocked: app is in READ-ONLY mode.');
                return;
            }
            try {
                await fetch('/api/sim/recover-alt', { method: 'POST' });
            } catch (e) {
                console.warn('recover-alt failed:', e);
            }
        },
        async beginDescent() {
            if (!this.services.simWriteEnabled) {
                console.warn('Begin-descent blocked: app is in READ-ONLY mode.');
                return;
            }
            // Use whatever the user has dialed into the FCU panel as the descent target,
            // falling back to the agent's configured default if it's still 10000+.
            try {
                await fetch('/api/sim/begin-descent', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ value: this.fcu.alt })
                });
            } catch (e) {
                console.warn('begin-descent failed:', e);
            }
        },
        // ---------- MCDU rendering ----------
        // Convert FBW MCDU tagged text (e.g. "{cyan}250{end}/{small}-5000{end}") into
        // sanitized HTML with coloured spans. Unknown tags are dropped; unmatched
        // {end} or open spans are auto-closed so a malformed line can't leak markup.
        renderMcdu(s) {
            if (!s) return '';
            const classes = {
                '{white}':   'mc-w',
                '{cyan}':    'mc-c',
                '{green}':   'mc-g',
                '{amber}':   'mc-a',
                '{magenta}': 'mc-m',
                '{small}':   'mc-sm',
                '{big}':     'mc-bg',
                '{right}':   'mc-rt',
                '{left}':    'mc-lt'
            };
            const tokens = s.split(/(\{[^}]+\})/);
            let html = '';
            let open = 0;
            for (const t of tokens) {
                if (!t) continue;
                if (t === '{end}') {
                    if (open > 0) { html += '</span>'; open--; }
                } else if (t === '{sp}') {
                    html += ' ';
                } else if (t.startsWith('{') && t.endsWith('}')) {
                    const cls = classes[t];
                    if (cls) {
                        html += '<span class="' + cls + '">';
                        open++;
                    }
                    // unknown tags get dropped silently
                } else {
                    // escape literal HTML special chars in the plain text content
                    html += t
                        .replace(/&/g, '&amp;')
                        .replace(/</g, '&lt;')
                        .replace(/>/g, '&gt;');
                }
            }
            while (open > 0) { html += '</span>'; open--; }
            return html;
        },
        // ---- FCU panel ----
        async fcuPost(path, value) {
            // Client-side read-only gate. The backend ALSO refuses to transmit
            // when Features.Sim.WriteEnabled is false — this just keeps the UI
            // from making no-op network requests and gives the user a hint.
            if (!this.services.simWriteEnabled) {
                console.warn('FCU write blocked: app is in READ-ONLY mode. Set Features.Sim.WriteEnabled=true in appsettings.json to take control.');
                return;
            }
            try {
                const opts = { method: 'POST', headers: { 'Content-Type': 'application/json' } };
                if (value !== undefined) opts.body = JSON.stringify({ value });
                await fetch('/api/fcu/' + path, opts);
            } catch (e) {
                console.warn('FCU POST failed', path, e);
            }
        },
        fcuStep(knob, delta) {
            if (!this.services.simWriteEnabled) {
                console.warn('FCU step blocked: app is in READ-ONLY mode.');
                return;
            }
            const fcu = this.fcu;
            if (knob === 'spd') {
                fcu.spd = Math.max(100, Math.min(399, fcu.spd + delta));
                this.fcuPost('spd/set', fcu.spd);
            } else if (knob === 'hdg') {
                fcu.hdg = ((fcu.hdg + delta) % 360 + 360) % 360;
                this.fcuPost('hdg/set', fcu.hdg);
            } else if (knob === 'alt') {
                fcu.alt = Math.max(100, Math.min(49000, fcu.alt + delta));
                this.fcuPost('alt/set', fcu.alt);
            } else if (knob === 'vs') {
                fcu.vs = Math.max(-6000, Math.min(6000, fcu.vs + delta));
                this.fcuPost('vs/set', fcu.vs);
            }
        },
        statusClass(value) {
            if (value === true) return 'ok';
            if (value === false) return 'off';
            return 'pending';
        },
        formatHeading(h) {
            if (h == null || isNaN(h)) return '---';
            const n = Math.round(((h % 360) + 360) % 360);
            return n.toString().padStart(3, '0');
        },
        convertSecondsToHMS(value) {
            if (value == null || isNaN(value) || value < 0) return;
            return new Date(value * 1000).toISOString().substr(11, 8);
        },
        convertHMSToSeconds(value) {
            if (!value || !/[0-9]{2}:[0-9]{2}:[0-9]{2}/.test(value)) return;
            const sub = value.split(":");
            return ((parseInt(sub[0]) * 60 + parseInt(sub[1])) * 60) + parseInt(sub[2]);
        },
        apDisplayName(value) {
            switch (value) {
                case "master": return "AP";
                case "flightDirector": return "FD";
                case "level": return "LVL";
                case "altitude": return "ALT";
                case "approach": return "APR";
                case "backcourse": return "BC";
                case "airspeed": return "SPD";
                case "mach": return "MCH";
                case "yawDamper": return "YD";
                case "autothrottle": return "AT";
                case "verticalHold": return "VS";
                case "heading": return "HDG";
                case "nav1": return "NAV";
                default: return value ? value.substr(0, 3).toUpperCase() : "—";
            }
        },
        searchAirports() {
            clearTimeout(this.searchTimeout);
            if (this.searchText.trim().length === 0) { this.searchResults = []; return; }
            this.searchTimeout = setTimeout(async () => {
                this.searchResults = [];
                let results = await this.db.airports.where("icao").equalsIgnoreCase(this.searchText).toArray();
                if (results.length === 0) {
                    const regex = new RegExp(this.searchText, "i");
                    results = await this.db.airports.filter(a => regex.test(a.name)).toArray();
                }
                this.searchResults.push(results);
            }, 500);
        },
        focusAirport(airport) {
            this.map.followButton.disable();
            this.showSearch = false;
            this.searchResults = [];
            this.searchText = "";
            this.map.map.setView([airport.geolocation.lat, airport.geolocation.lon], 13);
            this.selectedAirport = airport;
        },
        async saveAlert() {
            const permission = await this.requestNotificationPermission();
            if (!permission) return;

            let value = this.alertForm.value;
            if (this.alertForm.property !== 'elapsed' && this.alertForm.property !== 'ete')
                value = parseInt(value);

            let operator = this.alertForm.operator;
            let unwatch = null;

            switch (this.alertForm.property) {
                case "elapsed":
                    value = this.convertHMSToSeconds(value) * 1000;
                    if (isNaN(value)) return;
                    const timeout = setTimeout(() => {
                        new Notification(`Elapsed time has passed.`);
                        this.alerts.splice(this.alerts.findIndex(x => x.type === "elapsed" && x.value === value && x.reference === timeout), 1);
                    }, value);
                    this.alerts.push({ name: `Elapsed time ${operator} ${this.convertSecondsToHMS(value / 1000)}`, type: "elapsed", value: value, reference: timeout });
                    break;
                case "ete":
                    value = this.convertHMSToSeconds(value);
                    if (isNaN(value)) return;
                    unwatch = this.$watch('acInfo.gpswpete', () => {
                        if ((operator === "equals" && this.acInfo.gpswpete === value) || (operator === "greater" && this.acInfo.gpswpete > value) || (operator === "less" && this.acInfo.gpswpete < value)) {
                            new Notification(`ETE time is now ${this.convertSecondsToHMS(this.acInfo.gpswpete)}`);
                            unwatch();
                            this.alerts.splice(this.alerts.findIndex(x => x.type === "ete" && x.value === value && x.reference === unwatch), 1);
                        }
                    }, { deep: true });
                    this.alerts.push({ name: `ETE ${operator} ${this.convertSecondsToHMS(value)}`, type: "ete", value: value, reference: unwatch });
                    break;
                case "wp":
                    unwatch = this.$watch('acInfo.gpsWaypointDistance', () => {
                        if ((operator === "equals" && Math.round(this.acInfo.gpsWaypointDistance / 1000) === value) || (operator === "greater" && Math.round(this.acInfo.gpsWaypointDistance / 1000) > value) || (operator === "less" && Math.round(this.acInfo.gpsWaypointDistance / 1000) < value)) {
                            new Notification(`WP distance is now ${Math.round(this.acInfo.gpsWaypointDistance / 1000)} km`);
                            unwatch();
                            this.alerts.splice(this.alerts.findIndex(x => x.type === "wp" && x.value === value && x.reference === unwatch), 1);
                        }
                    }, { deep: true });
                    this.alerts.push({ name: `WP distance ${operator} ${value} km`, type: "wp", value: value, reference: unwatch });
                    break;
                case "airspeed":
                    unwatch = this.$watch('acInfo.airspeedIndicated', () => {
                        if ((operator === "equals" && Math.round(this.acInfo.airspeedIndicated) === value) || (operator === "greater" && Math.round(this.acInfo.airspeedIndicated) > value) || (operator === "less" && Math.round(this.acInfo.airspeedIndicated) < value)) {
                            new Notification(`Airspeed is ${Math.round(this.acInfo.airspeedIndicated)} kts`);
                            unwatch();
                            this.alerts.splice(this.alerts.findIndex(x => x.type === "airspeed" && x.value === value && x.reference === unwatch), 1);
                        }
                    }, { deep: true });
                    this.alerts.push({ name: `Airspeed ${operator} ${value} kts`, type: "airspeed", value: value, reference: unwatch });
                    break;
                case "altitude":
                    unwatch = this.$watch('acInfo.altitude', () => {
                        if ((operator === "equals" && Math.round(this.acInfo.altitude) === value) || (operator === "greater" && Math.round(this.acInfo.altitude) > value) || (operator === "less" && Math.round(this.acInfo.altitude) < value)) {
                            new Notification(`Altitude is ${Math.round(this.acInfo.altitude)} ft`);
                            unwatch();
                            this.alerts.splice(this.alerts.findIndex(x => x.type === "altitude" && x.value === value && x.reference === unwatch), 1);
                        }
                    }, { deep: true });
                    this.alerts.push({ name: `Altitude ${operator} ${value} ft`, type: "altitude", value: value, reference: unwatch });
                    break;
                case "fuel":
                    unwatch = this.$watch('acInfo.currentFuel', () => {
                        if ((operator === "equals" && this.fuelQuantityPercent === value) || (operator === "greater" && this.fuelQuantityPercent > value) || (operator === "less" && this.fuelQuantityPercent < value)) {
                            new Notification(`Fuel is ${this.fuelQuantityPercent}%`);
                            unwatch();
                            this.alerts.splice(this.alerts.findIndex(x => x.type === "fuel" && x.value === value && x.reference === unwatch), 1);
                        }
                    }, { deep: true });
                    this.alerts.push({ name: `Fuel ${operator} ${value}%`, type: "fuel", value: value, reference: unwatch });
                    break;
            }

            this.alertForm.value = null;
            this.showAlerts = false;
        },
        removeAlert(alert) {
            switch (alert.type) {
                case "elapsed": clearTimeout(alert.value); break;
                default: alert.reference(); break;
            }
            this.alerts.splice(this.alerts.findIndex(x => x.value === alert.value), 1);
        },
        async requestNotificationPermission() {
            if (Notification.permission === "granted") return true;
            const permission = await Notification.requestPermission();
            return permission === "granted";
        }
    },
    computed: {
        fuelQuantityPercent() {
            if (!this.acInfo || !this.acInfo.totalFuel) return 0;
            return Math.round(this.acInfo.currentFuel / this.acInfo.totalFuel * 100);
        },
        vatsimDotClass() {
            const n = this.vatsim.controllers.length;
            return n > 0 ? 'ok' : 'pending';
        },
        groupedVatsim() {
            const order = ['DEL', 'GND', 'TWR', 'APP', 'DEP', 'CTR', 'FSS', 'ATIS', 'OBS'];
            const groups = {};
            for (const c of this.vatsim.controllers) {
                const key = c.facilityShort || 'OBS';
                (groups[key] = groups[key] || []).push(c);
            }
            const result = [];
            for (const f of order) {
                if (groups[f]) {
                    result.push({ facility: f, list: groups[f] });
                    delete groups[f];
                }
            }
            for (const f of Object.keys(groups)) {
                result.push({ facility: f, list: groups[f] });
            }
            return result;
        },
        hasFma() {
            // FBW A32NX module produces a non-zero vertical, lateral, or autothrust mode integer.
            const a = this.acInfo;
            if (!a) return false;
            return !!(a.a32nxFmaVerticalMode || a.a32nxFmaLateralMode || a.a32nxAutothrustMode);
        },
        hasMcdu() {
            // SimBridge is sending us a non-null MCDU snapshot.
            return !!this.mcdu && !!this.mcdu.left;
        },
        // BRG status dot: green when MCDU frames are streaming, amber when the
        // socket is up but the aircraft has not yet pushed any data, red otherwise.
        simBridgeDotClass() {
            const s = this.services && this.services.simBridgeStatus;
            if (s === 'Streaming')         return 'ok';
            if (s === 'AwaitingAircraft')  return 'pending';
            return 'off';
        },
        // Human-readable explanation when the MCDU pane is empty — kept up to
        // date by SimBridgeClient.Status on the server.
        mcduOfflineHint() {
            const s = this.services && this.services.simBridgeStatus;
            if (s === 'Streaming')         return 'Reconnecting...';
            if (s === 'AwaitingAircraft')  return 'SimBridge is up. Open the MCDU in the cockpit, then in MCDU MENU → A32NX OPTIONS (or A339X / A333X OPTIONS) make sure "SimBridge" / "Remote MCDU" is ENABLED. Persistent setting CONFIG_SIMBRIDGE_ENABLED must be on.';
            if (s === 'Connecting')        return 'Reaching FlyByWire / Headwind SimBridge at ws://localhost:8380. Make sure SimBridge is running on the host PC.';
            return 'Set Features.SimBridge.Enabled = true in appsettings.json and restart.';
        },
        // Plain-English description of what the autoflight system is currently doing.
        // Synthesised from the FBW FMA modes and generic SimConnect autopilot bits so it
        // works for both A32NX/A330 (rich data) and stock aircraft (basic data).
        apSummary() {
            const a = this.acInfo;
            if (!a) return 'No data';
            // FBW path — we have rich pitch/roll mode strings.
            if (a.fmaPitch || a.fmaRoll) {
                const parts = [];
                // What the plane is doing vertically
                const v = a.fmaPitch;
                if (v === 'CLB')           parts.push('CLIMB (managed)');
                else if (v === 'OP CLB')   parts.push('OPEN CLIMB');
                else if (v === 'DES')      parts.push('DESCENT (managed)');
                else if (v === 'OP DES')   parts.push('OPEN DESCENT');
                else if (v === 'ALT')      parts.push('HOLDING ALTITUDE');
                else if (v === 'ALT*')     parts.push('CAPTURING ALTITUDE');
                else if (v === 'ALT CST')  parts.push('AT ALT CONSTRAINT');
                else if (v === 'V/S')      parts.push('VERTICAL SPEED');
                else if (v === 'FPA')      parts.push('FLIGHT PATH ANGLE');
                else if (v === 'SRS')      parts.push('TAKEOFF SRS');
                else if (v === 'SRS GA')   parts.push('GO-AROUND SRS');
                else if (v === 'G/S' || v === 'G/S*') parts.push('ON GLIDESLOPE');
                else if (v === 'LAND')     parts.push('LANDING');
                else if (v === 'FLARE')    parts.push('FLARE');
                else if (v)                parts.push(v);
                // What the plane is doing laterally
                const l = a.fmaRoll;
                if (l === 'NAV')           parts.push('following route');
                else if (l === 'HDG')      parts.push('heading mode');
                else if (l === 'TRK')      parts.push('track mode');
                else if (l === 'LOC' || l === 'LOC*') parts.push('on localizer');
                else if (l === 'RWY')      parts.push('runway track');
                else if (l)                parts.push(l.toLowerCase());
                return parts.length ? parts.join(', ').toUpperCase() : 'AUTOFLIGHT ARMED';
            }
            // Stock SimConnect fallback — best-effort from the boolean bits.
            const ap = a.autopilot || {};
            if (!ap.master && !ap.flightDirector) return 'HAND FLYING';
            const bits = [];
            if (ap.altitude || ap.verticalHold) bits.push('HOLDING ALTITUDE');
            if (ap.heading) bits.push('heading mode');
            if (ap.nav1)    bits.push('on nav');
            if (ap.approach) bits.push('approach armed');
            if (ap.airspeed) bits.push('speed hold');
            return bits.length ? bits.join(', ').toUpperCase() : (ap.master ? 'AP ENGAGED' : 'FD ONLY');
        },
        apSummaryIcon() {
            const v = this.acInfo && this.acInfo.fmaPitch;
            if (v === 'CLB' || v === 'OP CLB' || v === 'SRS' || v === 'SRS GA') return 'flight_takeoff';
            if (v === 'DES' || v === 'OP DES') return 'flight_land';
            if (v === 'ALT' || v === 'ALT*' || v === 'ALT CST') return 'horizontal_rule';
            if (v === 'LAND' || v === 'FLARE' || v === 'G/S' || v === 'G/S*') return 'flight_land';
            const ap = this.acInfo && this.acInfo.autopilot;
            if (ap && ap.master) return 'auto_mode';
            return 'pan_tool';
        },
        fuelClass() {
            const p = this.fuelQuantityPercent;
            if (p <= 10) return 'crit';
            if (p <= 25) return 'warn';
            return 'ok';
        },
        autopilotProperties() {
            if (!this.acInfo || !this.acInfo.autopilot) return {};
            let temp = JSON.parse(JSON.stringify(this.acInfo.autopilot));
            delete temp.available;
            return temp;
        },
        phase() {
            if (!this.acInfo || !this.acInfo.flightPhase) return 'Preflight';
            return this.acInfo.flightPhase;
        },
        phaseClass() {
            return this.phase.toLowerCase();
        },
        phaseIcon() {
            switch (this.phase) {
                case 'Preflight': return 'flight';
                case 'Taxi': return 'directions';
                case 'Takeoff': return 'flight_takeoff';
                case 'Climb': return 'trending_up';
                case 'Cruise': return 'paragliding';
                case 'Descent': return 'trending_down';
                case 'Approach': return 'flight_land';
                case 'Go-Around': return 'replay';
                case 'Landed': return 'check_circle';
                default: return 'flight';
            }
        },
        vsArrow() {
            const v = this.acInfo && this.acInfo.verticalSpeedFpm || 0;
            if (v > 100) return '▲';
            if (v < -100) return '▼';
            return '—';
        },
        vsClass() {
            const v = this.acInfo && this.acInfo.verticalSpeedFpm || 0;
            if (v > 100) return 'vs-up';
            if (v < -100) return 'vs-down';
            return 'vs-level';
        },
        compassTicks() {
            // Simple repeating compass labels every 30°
            const ticks = [];
            for (let i = 0; i < 360; i += 30) {
                if (i === 0) ticks.push('N');
                else if (i === 90) ticks.push('E');
                else if (i === 180) ticks.push('S');
                else if (i === 270) ticks.push('W');
                else ticks.push(i.toString());
            }
            return ticks;
        },
        showRadioAlt() {
            return this.acInfo && typeof this.acInfo.radioAltitudeFeet === 'number'
                && this.acInfo.radioAltitudeFeet > 0
                && this.acInfo.radioAltitudeFeet < 2500;
        },
        altDisplay() {
            // Mirror the Airbus PFD altitude window. INDICATED ALTITUDE respects the
            // baro setting, so when the pilot is on STD (1013.25) the value already
            // equals pressure altitude — we just format it as a flight level above
            // the typical transition altitude (~18000 ft in cruise).
            const alt = (this.acInfo && this.acInfo.altitude) || 0;
            if (alt >= 18000) {
                const fl = Math.round(alt / 100);
                return { value: 'FL' + fl.toString().padStart(3, '0'), unit: '' };
            }
            return { value: Math.round(alt).toLocaleString(), unit: 'ft' };
        },
        windDisplay() {
            if (!this.acInfo) return '—';
            const dir = Math.round(((this.acInfo.windDirectionDegrees || 0) % 360 + 360) % 360);
            const kts = Math.round(this.acInfo.windVelocityKnots || 0);
            return dir.toString().padStart(3, '0') + '° / ' + kts + 'kt';
        },
        oatDisplay() {
            if (!this.acInfo) return '—';
            const c = this.acInfo.outsideAirTempC;
            if (c === undefined || c === null) return '—';
            return Math.round(c) + '°C';
        },
        gDisplay() {
            const g = this.acInfo && this.acInfo.gForce;
            if (g === undefined || g === null) return '—';
            return g.toFixed(2) + 'g';
        },
        gClass() {
            const g = (this.acInfo && this.acInfo.gForce) || 1;
            if (g > 2.0 || g < 0.3) return 'g-warn';
            if (g > 1.5 || g < 0.7) return 'g-caution';
            return '';
        },
        flapNotches() {
            // A32NX uses 1/1+F label sharing for handle index 1
            return this.acInfo && this.acInfo.a32nxFwcFlightPhase > 0
                ? ['1', '2', '3', 'F']
                : ['1', '2', '3', 'F'];
        },
        flapNotation() {
            const i = this.acInfo && this.acInfo.flapsHandleIndex;
            if (i == null) return '—';
            switch (i) {
                case 0: return '0 / UP';
                case 1: return this.acInfo.a32nxFwcFlightPhase > 0 ? '1 / 1+F' : '1';
                case 2: return '2';
                case 3: return '3';
                case 4: return 'FULL';
                default: return String(i);
            }
        },
        autobrakeLabel() {
            const m = this.acInfo && this.acInfo.a32nxAutobrakesMode;
            if (m == null || this.acInfo.a32nxFwcFlightPhase === 0) return null;
            switch (m) {
                case 0: return 'OFF';
                case 1: return 'LO';
                case 2: return 'MED';
                case 3: return 'MAX';
                default: return null;
            }
        },
        thrustLimitLabel() {
            const t = this.acInfo && this.acInfo.a32nxThrustLimitType;
            if (!t || this.acInfo.a32nxFwcFlightPhase === 0) return null;
            switch (t) {
                case 1: return 'CLB';
                case 2: return 'MCT';
                case 3: return 'FLX';
                case 4: return 'TOGA';
                case 5: return 'REV';
                default: return null;
            }
        }
    }
});
