using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSFSFlightFollowing.AgentsCore;
using MSFSFlightFollowing.Models;

namespace MSFSFlightFollowing.SimConnect.Detectors;

/// <summary>
/// Watches the airspeed/altitude/vertical-speed of each <see cref="AircraftSnapshot"/>
/// and derives the current flight phase. Publishes <see cref="FlightPhaseChanged"/>
/// only when the phase actually changes — keeping bus traffic low.
/// </summary>
public sealed class FlightPhaseDetector
{
    private readonly IAgentBus _bus;
    private readonly ILogger<FlightPhaseDetector> _logger;
    private string _current = "Preflight";
    private bool _hasBeenAirborne;

    public FlightPhaseDetector(IAgentBus bus, ILogger<FlightPhaseDetector> logger)
    {
        _bus = bus;
        _logger = logger;
        _bus.Subscribe<AircraftSnapshot>(OnSnapshot);
        _bus.Subscribe<SimDisconnected>(OnDisconnected);
    }

    public string Current => _current;

    private async Task OnSnapshot(AircraftSnapshot snap)
    {
        var phase = Derive(snap.Aircraft);
        snap.Aircraft.FlightPhase = phase;
        if (phase == _current) return;

        var previous = _current;
        _current = phase;
        _logger.LogInformation("Flight phase: {Prev} -> {Next}", previous, phase);
        await _bus.PublishAsync(new FlightPhaseChanged(previous, phase));
    }

    private Task OnDisconnected(SimDisconnected _)
    {
        _hasBeenAirborne = false;
        _current = "Preflight";
        return Task.CompletedTask;
    }

    private static readonly string[] FmgcPhaseNames =
    {
        "Preflight", // 0 PREFLIGHT
        "Takeoff",   // 1 TAKEOFF
        "Climb",     // 2 CLIMB
        "Cruise",    // 3 CRUISE
        "Descent",   // 4 DESCENT
        "Approach",  // 5 APPROACH
        "Go-Around", // 6 GOAROUND
        "Landed"     // 7 DONE
    };

    private string Derive(AircraftStatusModel s)
    {
        // When the FlyByWire A32NX module is loaded we trust its FMGC phase —
        // it knows things we cannot infer (e.g. CRUISE vs DESC during a level-off).
        // Otherwise fall back to our generic VS/airspeed/altitude heuristic.
        if (s.IsA32nx)
        {
            var idx = s.A32nxFmgcFlightPhase;
            if (idx >= 0 && idx < FmgcPhaseNames.Length)
            {
                if (idx >= 1) _hasBeenAirborne = true; // moves to a flying phase
                return FmgcPhaseNames[idx];
            }
        }

        double speed = s.AirspeedIndicated;
        double vsi = s.VerticalSpeedFpm;
        double alt = s.Altitude;

        if (!_hasBeenAirborne && speed > 60 && Math.Abs(vsi) > 200)
            _hasBeenAirborne = true;

        if (!_hasBeenAirborne)
        {
            if (speed < 2) return "Preflight";
            if (speed < 40) return "Taxi";
            return "Takeoff";
        }

        if (speed < 40) { _hasBeenAirborne = false; return "Landed"; }
        if (vsi > 500 && alt < 1500) return "Takeoff";
        if (vsi > 300) return "Climb";
        if (vsi < -300 && alt < 4000) return "Approach";
        if (vsi < -300) return "Descent";
        return "Cruise";
    }
}
