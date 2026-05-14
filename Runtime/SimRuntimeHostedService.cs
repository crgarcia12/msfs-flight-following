using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MSFSFlightFollowing.AgentsCore;
using MSFSFlightFollowing.SimConnect.Detectors;

namespace MSFSFlightFollowing.Runtime;

/// <summary>
/// Owns the deterministic startup order of the cockpit runtime:
/// detectors and bridges subscribe first (so they never miss a message),
/// then the agents subscribe, and finally we connect to SimConnect and start
/// the SimBridge MCDU client. Also runs a background loop that keeps trying
/// to reconnect while MSFS is not running.
/// </summary>
public sealed class SimRuntimeHostedService : IHostedService
{
    private static readonly System.TimeSpan ReconnectInterval = System.TimeSpan.FromSeconds(5);

    private readonly MSFSFlightFollowing.SimConnect.SimConnector _sim;
    private readonly SimBridgeClient _simBridge;
    private readonly FlightPhaseDetector _phase;
    private readonly AltitudeCalloutEmitter _altitude;
    private readonly TakeoffDetector _takeoff;
    private readonly SignalRAgentBridge _signalRBridge;
    private readonly VatsimSignalRBridge _vatsimBridge;
    private readonly IEnumerable<AgentBase> _agents;
    private readonly ILogger<SimRuntimeHostedService> _logger;
    private readonly CancellationTokenSource _reconnectCts = new();

    public SimRuntimeHostedService(
        MSFSFlightFollowing.SimConnect.SimConnector sim,
        SimBridgeClient simBridge,
        FlightPhaseDetector phase,
        AltitudeCalloutEmitter altitude,
        TakeoffDetector takeoff,
        SignalRAgentBridge signalRBridge,
        VatsimSignalRBridge vatsimBridge,
        IEnumerable<AgentBase> agents,
        ILogger<SimRuntimeHostedService> logger)
    {
        _sim = sim;
        _simBridge = simBridge;
        _phase = phase;
        _altitude = altitude;
        _takeoff = takeoff;
        _signalRBridge = signalRBridge;
        _vatsimBridge = vatsimBridge;
        _agents = agents;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Resolving the constructor parameters above already executed every
        // Subscribe<T> call on the bus, so by the time we reach this point
        // every detector, the SignalR bridge, and every agent is wired up.
        int agentCount = 0;
        foreach (var a in _agents) agentCount++;
        _logger.LogInformation(
            "Cockpit runtime ready: phase detector, altitude callouts, takeoff detector, SignalR bridge, {AgentCount} agents.",
            agentCount);

        _sim.Connect();
        _ = _simBridge.Connect();
        _ = Task.Run(() => ReconnectLoopAsync(_reconnectCts.Token));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _reconnectCts.Cancel();
        _sim.Disconnect();
        MessageWindow.GetWindow().Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// While running, if the sim isn't connected, try every <see cref="ReconnectInterval"/>.
    /// This lets the user launch the web UI before MSFS without losing the connection.
    /// Also retries the SimBridge MCDU socket so the panel auto-recovers if SimBridge
    /// is started/restarted after the app, or if the WebSocket drops.
    /// </summary>
    private async Task ReconnectLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(ReconnectInterval, ct).ConfigureAwait(false);
                if (!_sim.IsConnected)
                {
                    try
                    {
                        _logger.LogDebug("Retrying SimConnect…");
                        _sim.Connect();
                    }
                    catch (System.Exception ex)
                    {
                        _logger.LogDebug(ex, "SimConnect reconnect attempt failed (will retry)");
                    }
                }

                if (_simBridge.Enabled && !_simBridge.IsConnected)
                {
                    try
                    {
                        _logger.LogDebug("Retrying SimBridge…");
                        await _simBridge.Connect().ConfigureAwait(false);
                    }
                    catch (System.Exception ex)
                    {
                        _logger.LogDebug(ex, "SimBridge reconnect attempt failed (will retry)");
                    }
                }
            }
        }
        catch (System.OperationCanceledException) { /* shutdown */ }
    }
}
