# MSFS Flight Following

A small **.NET 8 / ASP.NET Core** web app that connects to **Microsoft Flight Simulator** via **SimConnect**, streams the aircraft state to a browser-based **glass-cockpit HUD + map** over SignalR, and runs a tiny **multi-agent** scenario engine (Pilot · Copilot · Navigator · Comms · Operations) that can react to the flight in real time.

> Forked from [kurt1288/msfs-flight-following](https://github.com/kurt1288/msfs-flight-following) and extended.

## Features

- 🛩  **Live aircraft tracking** on a dark Leaflet map with a fading breadcrumb trail and great-circle leg to the next GPS waypoint
- 📟  **Glass-cockpit HUD** — IAS/GS/TAS, ALT, VS (computed), heading tape, fuel bar, autopilot strip, flight-phase pill (`Preflight · Taxi · Takeoff · Climb · Cruise · Descent · Approach · Go-Around · Landed`)
- 🌬  **Environment block** — wind direction & velocity, OAT, G-load (color-coded)
- ⚙  **Config block** — flaps notch indicator (0/1/2/3/FULL), `AIR/GND`, spoilers, autobrake, A32NX thrust limit (CLB/MCT/FLX/TOGA)
- 📡  **Radio altitude** inline when below 2 500 ft AGL
- 🔌  **Connection badges** — `SIM` (SimConnect), `BRG` (FlyByWire SimBridge / MCDU), `CLD` (Azure Event Hub)
- 🔄  **Auto-reconnect** — if MSFS isn't running yet, the app keeps retrying every 5 s in the background
- 🗺  **~37 000 airport** overlay (OpenAIP) with ICAO/name search, runways and frequencies
- 🤖  **Agent panel** — collapsible chat-style timeline with one color per agent
- 🔔  **Threshold alerts** (altitude / airspeed / fuel / ETE / WP distance / elapsed time) via browser notifications
- 🎛  **Touch-friendly FCU panel** — clickable SPD/HDG/ALT/V/S knobs and AP1/AP2/A·THR/LOC/APPR/EXPED buttons. Drives the FlyByWire A32NX **and** the Headwind A330-900 from any browser on the LAN (phone, tablet, second monitor). See [Multi-airframe FCU](#multi-airframe-fcu) below.
- ⤵️ **Auto-descent at T/D** — the moment the FMS raises "T/D REACHED" on the PFD, the app dials in the configured descent altitude on the FCU and pushes ALT for managed descent. Works on the A32NX and Headwind A330. Configurable; manual `BEGIN DESCENT` button on both views.
- 🧩  Optional integrations: **FlyByWire A32NX MCDU** (auto-reprogram), **Azure Event Hub** sink — both gated by config

### FlyByWire A32NX awareness

When the [FlyByWire A32NX](https://github.com/flybywiresim/aircraft) aircraft is loaded, additional L:vars are picked up:

| L:var | What it does |
|---|---|
| `A32NX_FMGC_FLIGHT_PHASE` | Overrides our generic flight-phase heuristic with the FMGC's view (incl. `Go-Around`) |
| `A32NX_FWC_FLIGHT_PHASE` | Used to detect that the FBW module is actually running (gates A32NX-only chips) |
| `A32NX_AUTOTHRUST_THRUST_LIMIT_TYPE` | CLB / MCT / FLX / TOGA / REV chip |
| `A32NX_AUTOBRAKES_ARMED_MODE` | OFF / LO / MED / MAX chip |
| `A32NX_SPOILERS_ARMED` | SPLR chip lights up |

These chips are hidden for any non-A32NX aircraft, so the HUD stays clean for everyone else.

## Quick start

```powershell
# Open the solution in Visual Studio 2022 and press F5 (Debug | x64)
# or, from the command line:
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  MSFSFlightFollowing.csproj /p:Configuration=Debug /p:Platform=x64
.\bin\x64\Debug\net8.0\MSFSFlightFollowing.exe
```

Then open <http://localhost:7777>. Other devices on your LAN can reach it via `http://<your-machine-name>:7777`.

> `dotnet build` does **not** work for this project — it uses a COM-aware reference resolution path. Use full-framework MSBuild from Visual Studio 2022.

### Prerequisites

| | Required for |
|---|---|
| Microsoft Flight Simulator (2020 or 2024) | Live data via SimConnect |
| Visual Studio 2022 (Community is fine) + .NET 8 SDK | Building |
| FlyByWire A32NX + local SimBridge on `:8380` | Optional MCDU reprogramming demo |
| Azure subscription + `az login` | Optional Event Hub telemetry sink |

## Configuration

All optional integrations are **off by default**. Edit `appsettings.json`:

```json
"Features": {
  "AzureEventHub": {
    "Enabled": false,
    "Namespace": "your-namespace.servicebus.windows.net",
    "Hub": "msfs"
  },
  "SimBridge": {
    "Enabled": false,
    "Url": "ws://localhost:8380/interfaces/v1/mcdu"
  },
  "Vatsim": {
    "Enabled": false,
    "PollIntervalSeconds": 20,
    "RangeBufferNm": 0
  },
  "Agents": { "Enabled": true }
}
```

- `Features.AzureEventHub.Enabled` — when `true`, every SimConnect sample is published to the named Event Hub using `AzureCliCredential` (needs prior `az login`). Auto-disables itself on the first error.
- `Features.SimBridge.Enabled` — when `true`, the `Comms` agent connects to FlyByWire SimBridge so the demo divert scenario can reprogram the A32NX MCDU.
- `Features.Vatsim.Enabled` — when `true`, polls the public [VATSIM JSON feeds](https://data.vatsim.net/v3/vatsim-data.json) every `PollIntervalSeconds` (minimum 15 s) and matches online ATC against your live position using the transceivers feed. Nearby controllers appear in the **VATSIM** panel on the left rail, and the `Comms` agent calls out new stations as they come into range. **ATIS text-to-speech** uses the browser's built-in `speechSynthesis` — click the speaker icon on any ATIS row to hear it.
- `Features.Agents.Enabled` — master switch for all autonomous agents (`Comms`, `Pilot`, `Navigator`, `Operations`, `Copilot`, `AltitudeGuard`, `DescentGuard`). When `false`, every agent short-circuits at the top of its handler. Keep `false` if you only want manual UI-driven sim writes (recommended).
- `Features.Sim.WriteEnabled` — hard write-guard at the SimConnect transmit choke point. When `false`, **every** `TransmitClientEvent` call is dropped and logged — nothing the app does can change a single switch in the sim. When `true`, UI button presses (and any agent that is enabled) can write to the sim. Defaults to `false` for safety.
- `Features.Descent.Enabled` — secondary guard for the auto-T/D descent feature. Even if `Agents.Enabled` is `true`, `DescentGuard` won't fire unless this is also `true`.

### Note on VATSIM voice (AFV)

VATSIM's real **AFV voice stream** requires a CID + password through the official AFV client library and is *not* exposed as an anonymous public stream. This integration therefore uses the data feeds + browser TTS to give you situational awareness ("who is online, on what frequency, where, and what's the ATIS") without needing VATSIM credentials. If you want real audio you'll need a separate AFV-compatible client (vPilot / Swift / xPilot).

## Multi-airframe FCU

The bottom-bar FCU panel sends every command **twice** — once as `A32NX.FCU_*` and once as `A339X.FCU_*`. MSFS silently drops the prefix that the loaded aircraft doesn't subscribe to, so a single button works for both the FlyByWire A32NX and the Headwind A330-900 without any in-app aircraft selection.

| Knob | Sets | Push/Pull |
|------|------|-----------|
| **SPD** | target IAS / Mach | PUSH = managed speed, PULL = selected |
| **HDG** | heading bug (°) | PUSH = NAV mode, PULL = HDG/TRK mode |
| **ALT** | target altitude (100 ft / 1000 ft increments) | PUSH = ALT capture, PULL = OP CLB / OP DES |
| **V/S** | vertical speed (±6000 fpm) | PUSH = level off, PULL = V/S mode |

Buttons: **AP 1**, **AP 2**, **A·THR**, **LOC**, **APPR**, **EXPED**.

The panel is reachable from any browser on the same network at `http://<your-pc>:7777` — handy for using a phone or tablet as a hardware-style autopilot panel while the sim runs on your main screen. The accompanying **FMA banner** in the HUD shows the live A32NX FMA modes (incl. `SRS` warnings) so you can verify the autopilot is actually doing what you asked.

The app also exposes a `POST /api/fcu/{verb}` HTTP surface — see `Controllers/FcuController.cs` — so external hardware boxes (Stream Deck, custom Arduino panel) can drive the autopilot the same way.

## Two front-ends: desktop & mobile

The app serves the same backend through **two purpose-built UIs**:

| URL | Best for | Layout |
|-----|----------|--------|
| `/` | Big screen / second monitor | Full HUD: gauges row, Leaflet map with trail + flight plan, agent log, VATSIM panel, FCU slide-up at the bottom. |
| `/m` *(also `/mobile`)* | Phone in your pocket / tablet on the lap | Three-tab single-page app: **Flight** (large telemetry tiles + FMA), **Map** (full-screen mini-map), **FCU** (full autopilot panel, always visible). |

Both share the **same SignalR hub** (`/ws`) and **same REST endpoints**, so opening the mobile view on a phone while the desktop view runs on your PC just shows you the same live state from two angles. The desktop HUD has a 📱 link in the top-right status badges; the mobile view has a 🖥 link in the top bar.

To use the mobile view while away from the PC, expose `http://<your-pc>:7777` through a tunnel of your choice (Tailscale, Cloudflare Tunnel, port-forward) — the app already binds `0.0.0.0`.

## Health endpoint

```
GET /healthz  →  { sim, simBridge, simBridgeEnabled, eventHubEnabled }
```

## Built-in demo scenario

`Comms` waits for the aircraft to be above 7 000 ft, then announces *“Stuttgart closed”*. `Operations` requests a new flight plan. `Navigator` proposes **LSZH RWY 28**. `Copilot` drives a 4 000 ft descent and (if SimBridge is enabled) reprograms the A32NX MCDU INIT/FPLN pages.

## Project layout

```
.
├── Program.cs / Startup.cs        # ASP.NET Core host + DI wiring
├── Controllers/HomeController.cs  # Index + /get/airports + /healthz
├── Models/                        # AircraftStatusModel, FeatureOptions, ClientData, WebSocketConnector (SignalR hub)
├── SimConnect/                    # SimConnector (ISimCommands), data definitions
│   └── Detectors/                 # FlightPhaseDetector, AltitudeCalloutEmitter, TakeoffDetector
├── SimBridge/                     # FlyByWire MCDU WebSocket client (ISimBridgeMcdu)
├── AgentsCore/                    # AgentBus (IAgentBus), AgentBase, AgentContext, typed Messages
├── Agents/                        # Comms, Operations, Navigator, Copilot, Pilot — bus subscribers
├── Runtime/                       # SimDataDispatcher (BackgroundService), SignalRAgentBridge, SimRuntimeHostedService
├── Views/Home/Index.cshtml        # Glass-cockpit HUD + map UI
├── wwwroot/js/main.js             # Vue 2 app + Leaflet map class
├── wwwroot/css/main.css           # Theme
├── Airports/                      # OpenAIP XML airport DB
└── EventHub.cs                    # Optional Azure sink
```

### How the cockpit pipeline fits together

```
 SimConnect (1 Hz)
     │  SnapshotReceived (event)
     ▼
 SimDataDispatcher ──► SignalR  /ws  ReceiveData      ──► browser HUD
     │           └──► EventHub.SendEventAsync (optional)
     │
     │  AgentBus.PublishAsync(AircraftSnapshot)
     ▼
 ┌─────────────┐  ┌──────────────────────┐  ┌───────────────┐
 │ FlightPhase │  │ AltitudeCallout      │  │ Takeoff       │
 │ Detector    │  │ Emitter (3k, 10k)    │  │ Detector      │
 └─────────────┘  └──────────────────────┘  └───────────────┘
     │ FlightPhaseChanged    │ AltitudeCallout        │ TakeoffStarted
     ▼                       ▼                        ▼
 ┌─────────────────────────  AgentBus  ───────────────────────────┐
 │  Comms ─► AtcMessage ─► Operations ─► DestinationAssigned      │
 │                                       │                         │
 │                                       ▼                         │
 │                                   Navigator ─► ApproachCleared  │
 │                                                 │               │
 │                                                 ▼               │
 │                                              Copilot ─► Sim     │
 │                                                  │  (descent,   │
 │                                                  │   MCDU,      │
 │                                                  │   APPR+AP)   │
 │                                                  ▼              │
 │                                              ChecklistCallout   │
 │                                                  │              │
 │                                                  ▼              │
 │                                              Pilot (echo +1s)   │
 └────────────────────────────────────────────────────────────────┘
     │ ChecklistCallout
     ▼
 SignalRAgentBridge ──► SignalR /ws  ReceiveAgentEvent ──► HUD timeline
```

## License

[MIT](LICENSE)

