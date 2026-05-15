using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FSUIPCWinformsAutoCS;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MSFSFlightFollowing.AgentsCore;
using MSFSFlightFollowing.Models;
using MSFSFlightFollowing.SimConnect;

namespace MSFSFlightFollowing.Runtime;

/// <summary>
/// Decouples the SimConnect message-pump thread from the side effects of a
/// snapshot (SignalR send, Event Hub send, agent bus publish, vertical-speed
/// derivation). The pump just writes the latest sample into a 1-slot channel;
/// the background loop here drains it and fans the snapshot out.
/// </summary>
public sealed class SimDataDispatcher : BackgroundService
{
    private readonly Channel<AircraftStatusModel> _channel = Channel.CreateBounded<AircraftStatusModel>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly MSFSFlightFollowing.SimConnect.SimConnector _sim;
    private readonly IHubContext<WebSocketConnector> _hub;
    private readonly EventHub _eventHub;
    private readonly SimBridgeClient _simBridge;
    private readonly IAgentBus _bus;
    private readonly ILogger<SimDataDispatcher> _logger;

    private double? _previousAltitude;
    private DateTime? _previousSampleAt;

    public SimDataDispatcher(
        MSFSFlightFollowing.SimConnect.SimConnector sim,
        IHubContext<WebSocketConnector> hub,
        EventHub eventHub,
        SimBridgeClient simBridge,
        IAgentBus bus,
        ILogger<SimDataDispatcher> logger)
    {
        _sim = sim;
        _hub = hub;
        _eventHub = eventHub;
        _simBridge = simBridge;
        _bus = bus;
        _logger = logger;

        _sim.SnapshotReceived += OnSnapshot;
        _sim.Disconnected += OnDisconnected;
    }

    private void OnSnapshot(object? sender, AircraftSnapshotEventArgs e)
    {
        // Cheap and non-blocking — safe to call from the SimConnect message-pump thread.
        _channel.Writer.TryWrite(e.Aircraft);
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        var disconnectedFrame = new ClientData
        {
            IsConnected = false,
            Services = SnapshotServices(simConnected: false)
        };
        // Fire and forget — we are on the pump thread.
        _ = _hub.Clients.All.SendAsync("ReceiveData", disconnectedFrame);
        _ = _bus.PublishAsync(new SimDisconnected());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Heartbeat: even when SimConnect isn't delivering aircraft data (e.g.
        // user is in the menu, or an LVar in the data definition is unavailable
        // for the current aircraft), we still push the live services state so
        // the UI shows the correct CONNECTED / NO SIM / NO DATA badge.
        _ = Task.Run(() => HeartbeatLoopAsync(stoppingToken), stoppingToken);

        try
        {
            await foreach (var aircraft in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                _lastAircraftAt = DateTime.UtcNow;
                EnrichWithVerticalSpeed(aircraft);

                // Detectors will overwrite FlightPhase before we send to clients;
                // PublishAsync awaits all subscribers in order.
                await _bus.PublishAsync(new AircraftSnapshot(aircraft));

                var clientData = new ClientData
                {
                    IsConnected = true,
                    Data = aircraft,
                    Services = SnapshotServices(simConnected: true),
                    Mcdu = _simBridge.CurrentScreen
                };

                try { await _hub.Clients.All.SendAsync("ReceiveData", clientData, stoppingToken); }
                catch (Exception ex) { _logger.LogWarning(ex, "SignalR ReceiveData send failed"); }

                _ = SendToEventHubAsync(clientData);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    private DateTime? _lastAircraftAt;
    private AircraftStatusModel? _lastAircraft;

    private async Task HeartbeatLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(2000, token).ConfigureAwait(false);
                // If we sent a real aircraft frame in the last 2.5 s, skip — the
                // data path already covers the UI.
                if (_lastAircraftAt.HasValue && (DateTime.UtcNow - _lastAircraftAt.Value).TotalSeconds < 2.5)
                    continue;

                // SimConnect handle is open but no data has arrived for a while.
                // Surface the real services state so the UI doesn't show stale "NO SIM".
                var frame = new ClientData
                {
                    IsConnected = _sim.IsConnected,
                    Data = _sim.LatestSnapshot,
                    Services = SnapshotServices(simConnected: _sim.IsConnected && _sim.LatestSnapshot != null),
                    Mcdu = _simBridge.CurrentScreen
                };
                try { await _hub.Clients.All.SendAsync("ReceiveData", frame, token); }
                catch (Exception ex) { _logger.LogDebug(ex, "Heartbeat send failed"); }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    private async Task SendToEventHubAsync(ClientData data)
    {
        try { await _eventHub.SendEventAsync(data); }
        catch (Exception ex) { _logger.LogWarning(ex, "EventHub send failed"); }
    }

    private void EnrichWithVerticalSpeed(AircraftStatusModel aircraft)
    {
        var now = DateTime.UtcNow;
        if (_previousAltitude.HasValue && _previousSampleAt.HasValue)
        {
            double seconds = (now - _previousSampleAt.Value).TotalSeconds;
            if (seconds > 0.05)
                aircraft.VerticalSpeedFpm = (aircraft.Altitude - _previousAltitude.Value) / seconds * 60.0;
        }
        _previousAltitude = aircraft.Altitude;
        _previousSampleAt = now;
    }

    private ServicesStatus SnapshotServices(bool simConnected) => new()
    {
        Sim = simConnected,
        SimBridge = _simBridge.IsConnected,
        EventHub = _eventHub.Enabled,
        SimBridgeStatus = _simBridge.Status.ToString(),
        SimWriteEnabled = _sim.WriteEnabled
    };
}
