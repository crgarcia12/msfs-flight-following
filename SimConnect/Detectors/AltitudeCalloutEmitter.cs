using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSFSFlightFollowing.AgentsCore;

namespace MSFSFlightFollowing.SimConnect.Detectors;

/// <summary>
/// Publishes a single <see cref="AltitudeCallout"/> when the aircraft first crosses
/// 3 000 ft or 10 000 ft in each direction during a flight.
/// State resets when the sim disconnects.
/// </summary>
public sealed class AltitudeCalloutEmitter
{
    private static readonly int[] Thresholds = { 3000, 10000 };

    private readonly IAgentBus _bus;
    private readonly ILogger<AltitudeCalloutEmitter> _logger;

    private double? _previousAltitude;
    private readonly bool[] _ascendingFired = new bool[Thresholds.Length];
    private readonly bool[] _descendingFired = new bool[Thresholds.Length];

    public AltitudeCalloutEmitter(IAgentBus bus, ILogger<AltitudeCalloutEmitter> logger)
    {
        _bus = bus;
        _logger = logger;
        _bus.Subscribe<AircraftSnapshot>(OnSnapshot);
        _bus.Subscribe<SimDisconnected>(OnDisconnected);
    }

    private async Task OnSnapshot(AircraftSnapshot snap)
    {
        double alt = snap.Aircraft.Altitude;
        if (_previousAltitude is null)
        {
            _previousAltitude = alt;
            return;
        }

        double prev = _previousAltitude.Value;
        _previousAltitude = alt;

        for (int i = 0; i < Thresholds.Length; i++)
        {
            int t = Thresholds[i];
            if (!_ascendingFired[i] && prev < t && alt >= t)
            {
                _ascendingFired[i] = true;
                _descendingFired[i] = false;
                _logger.LogInformation("Altitude callout: passing {Feet} ft climbing", t);
                await _bus.PublishAsync(new AltitudeCallout(t, Ascending: true));
            }
            else if (!_descendingFired[i] && prev > t && alt <= t)
            {
                _descendingFired[i] = true;
                _ascendingFired[i] = false;
                _logger.LogInformation("Altitude callout: passing {Feet} ft descending", t);
                await _bus.PublishAsync(new AltitudeCallout(t, Ascending: false));
            }
        }
    }

    private Task OnDisconnected(SimDisconnected _)
    {
        _previousAltitude = null;
        for (int i = 0; i < Thresholds.Length; i++)
        {
            _ascendingFired[i] = false;
            _descendingFired[i] = false;
        }
        return Task.CompletedTask;
    }
}
