using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSFSFlightFollowing.AgentsCore;

namespace MSFSFlightFollowing.SimConnect.Detectors;

/// <summary>
/// Emits a single <see cref="TakeoffStarted"/> the first time indicated airspeed
/// rises above 5 kt while still on (or just leaving) the ground.
/// Resets when the sim disconnects.
/// </summary>
public sealed class TakeoffDetector
{
    private readonly IAgentBus _bus;
    private readonly ILogger<TakeoffDetector> _logger;
    private bool _fired;

    public TakeoffDetector(IAgentBus bus, ILogger<TakeoffDetector> logger)
    {
        _bus = bus;
        _logger = logger;
        _bus.Subscribe<AircraftSnapshot>(OnSnapshot);
        _bus.Subscribe<SimDisconnected>(OnDisconnected);
    }

    private async Task OnSnapshot(AircraftSnapshot snap)
    {
        if (_fired) return;
        if (snap.Aircraft.AirspeedIndicated <= 5) return;

        _fired = true;
        _logger.LogInformation("Takeoff detected (airspeed > 5 kt).");
        await _bus.PublishAsync(new TakeoffStarted());
    }

    private Task OnDisconnected(SimDisconnected _)
    {
        _fired = false;
        return Task.CompletedTask;
    }
}
