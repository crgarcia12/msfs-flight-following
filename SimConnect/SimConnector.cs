using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FlightSimulator.SimConnect;
using MSFSFlightFollowing.Models;
using static MSFSFlightFollowing.Models.SimConnectStructs;

namespace MSFSFlightFollowing.SimConnect;

/// <summary>
/// Owns the SimConnect lifecycle and exposes a clean event-based contract:
/// <list type="bullet">
///   <item><see cref="SnapshotReceived"/> fires once per sample (1 Hz).</item>
///   <item><see cref="Disconnected"/> fires when SimConnect drops or we shut down.</item>
///   <item>Implements <see cref="ISimCommands"/> for the autopilot commands the agents need.</item>
/// </list>
/// Has no knowledge of SignalR, EventHub, or the agent bus — those are wired up by
/// <see cref="MSFSFlightFollowing.Runtime.SimDataDispatcher"/>.
/// </summary>
public sealed class SimConnector : ISimCommands, IDisposable
{
    private const uint WM_USER_SIMCONNECT = 0x0402;

    private readonly ILogger<SimConnector> _logger;
    private readonly IntPtr _windowHandle;
    private readonly Func<bool> _hasConnectedClients;
    private readonly Microsoft.Extensions.Options.IOptions<MSFSFlightFollowing.Models.FeatureOptions>? _featureOptions;
    private CancellationTokenSource _cts = new();

    private Microsoft.FlightSimulator.SimConnect.SimConnect? _simconnect;
    private AircraftStatusModel? _latestSnapshot;

    public event EventHandler<AircraftSnapshotEventArgs>? SnapshotReceived;
    public event EventHandler? Disconnected;

    public bool IsConnected => _simconnect != null;
    public AircraftStatusModel? LatestSnapshot => _latestSnapshot;

    /// <summary>
    /// When <c>false</c>, every <see cref="ISimCommands"/> verb is a no-op
    /// (still returns success at the HTTP layer, just doesn't transmit to MSFS).
    /// Driven by <c>Features.Sim.WriteEnabled</c> in appsettings.json.
    /// </summary>
    public bool WriteEnabled => _featureOptions?.Value.Sim.WriteEnabled ?? false;

    public SimConnector(
        ILogger<SimConnector> logger,
        IHostApplicationLifetime lifetime,
        Microsoft.Extensions.Options.IOptions<MSFSFlightFollowing.Models.FeatureOptions> featureOptions,
        Func<bool>? hasConnectedClients = null)
    {
        _logger = logger;
        _featureOptions = featureOptions;
        _hasConnectedClients = hasConnectedClients ?? WebSocketConnector.HasConnectedClients;

        var win = MessageWindow.GetWindow();
        _windowHandle = win.Hwnd;
        win.WndProcHandle += OnWndProc;

        lifetime.ApplicationStopping.Register(Disconnect);
    }

    public void Connect()
    {
        if (_simconnect != null) return;

        // Replace the CTS — a previous Disconnect() will have cancelled it.
        if (_cts.IsCancellationRequested)
        {
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }

        try
        {
            _simconnect = new Microsoft.FlightSimulator.SimConnect.SimConnect(
                "MSFS Flight Following", _windowHandle, WM_USER_SIMCONNECT, null, 0);

            _simconnect.OnRecvOpen += OnRecvOpen;
            _simconnect.OnRecvQuit += OnRecvQuit;
            _simconnect.OnRecvException += OnRecvException;
            _simconnect.OnRecvSimobjectDataBytype += OnRecvSimobjectDataByType;
        }
        catch (COMException)
        {
            // Common when MSFS isn't running. Caller (reconnect loop) will retry quietly.
            _simconnect = null;
        }
    }

    public void Disconnect()
    {
        if (_simconnect == null)
        {
            // Still raise the event so the front-end can clear its UI even when
            // SimConnect never connected.
            Disconnected?.Invoke(this, EventArgs.Empty);
            return;
        }

        _cts.Cancel();
        _simconnect.Dispose();
        _simconnect = null;
        _logger.LogInformation("SimConnect was disconnected from the flight sim.");
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Disconnect();

    // ---------- SimConnect message-pump plumbing ----------

    private IntPtr OnWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == WM_USER_SIMCONNECT) _simconnect?.ReceiveMessage();
        }
        catch
        {
            Disconnect();
        }
        return IntPtr.Zero;
    }

    private void OnRecvOpen(Microsoft.FlightSimulator.SimConnect.SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        SimConnectDataDefinitions.Register(_simconnect!);
        MapClientEvents();
        _ = Task.Run(PollLoopAsync);
        _logger.LogInformation("Simconnect has connected to the flight sim.");
    }

    private void OnRecvQuit(Microsoft.FlightSimulator.SimConnect.SimConnect sender, SIMCONNECT_RECV data)
    {
        Disconnect();
    }

    private void OnRecvException(Microsoft.FlightSimulator.SimConnect.SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        _logger.LogError("SimConnect exception: {Code}", data.dwException);
        Disconnect();
    }

    private async Task PollLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                await Task.Delay(1000, _cts.Token).ConfigureAwait(false);
                if (_hasConnectedClients())
                {
                    _simconnect?.RequestDataOnSimObjectType(
                        DATA_REQUEST.AircraftStatus, DEFINITIONS.AircraftStatus, 0,
                        SIMCONNECT_SIMOBJECT_TYPE.USER);
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    private void OnRecvSimobjectDataByType(
        Microsoft.FlightSimulator.SimConnect.SimConnect sender,
        SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
    {
        if (data.dwRequestID != (uint)DATA_REQUEST.AircraftStatus) return;

        var model = new AircraftStatusModel((AircraftStatusStruct)data.dwData[0]);
        _latestSnapshot = model;
        try
        {
            SnapshotReceived?.Invoke(this, new AircraftSnapshotEventArgs(model));
        }
        catch (Exception ex)
        {
            // Never let a downstream handler crash the SimConnect pump.
            _logger.LogWarning(ex, "SnapshotReceived handler threw");
        }
    }

    // ---------- ISimCommands ----------

    private enum ClientEvent
    {
        // Legacy fixed-ID slots kept so we don't have to migrate the few non-FCU mappings.
        Unpause = 0,
        Pause,
        GearUp,
        GearDown,
    }

    private enum NotificationGroup
    {
        Default = 0
    }

    // Dynamic event mapping. SimConnect's MapClientEventToSimEvent only cares about
    // the numeric value of the enum we pass it, so we can mint new IDs at runtime
    // from a counter and cast them through this single-member enum.
    private enum DynamicId : uint { Zero = 0 }

    private readonly System.Collections.Generic.Dictionary<string, uint> _eventIdByName = new();
    private uint _nextDynamicEventId = 10_000;

    private void MapClientEvents()
    {
        var sim = _simconnect!;
        // Only one legacy fixed mapping; everything else is mapped lazily through TransmitEventByName.
        sim.MapClientEventToSimEvent(ClientEvent.GearDown, "TOGGLE_BEACON_LIGHTS");
    }

    private uint EnsureMapped(string eventName)
    {
        if (_eventIdByName.TryGetValue(eventName, out var id)) return id;
        var sim = _simconnect;
        if (sim == null) return 0;
        id = _nextDynamicEventId++;
        sim.MapClientEventToSimEvent((DynamicId)id, eventName);
        _eventIdByName[eventName] = id;
        return id;
    }

    private void TransmitEventByName(string eventName, uint payload = 0)
    {
        var sim = _simconnect;
        if (sim == null) return;
        // Hard write-guard: when the user has not explicitly enabled write
        // mode, drop every transmit so opening the app cannot move knobs.
        if (!WriteEnabled)
        {
            _logger.LogInformation("Sim write skipped (Features.Sim.WriteEnabled = false): {Event} payload={Payload}", eventName, payload);
            return;
        }
        var id = EnsureMapped(eventName);
        sim.TransmitClientEvent(
            Microsoft.FlightSimulator.SimConnect.SimConnect.SIMCONNECT_OBJECT_ID_USER,
            (DynamicId)id,
            payload,
            NotificationGroup.Default,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
    }

    /// <summary>Transmits the same FCU command twice — once as A32NX.&lt;suffix&gt; and
    /// once as A339X.&lt;suffix&gt;. This way a single command works for both the
    /// FlyByWire A32NX and the Headwind A330-900; MSFS silently ignores the prefix
    /// the loaded aircraft doesn't subscribe to.</summary>
    private void TransmitFcu(string suffix, uint payload = 0)
    {
        TransmitEventByName("A32NX." + suffix, payload);
        TransmitEventByName("A339X." + suffix, payload);
    }

    private void Transmit(ClientEvent ev, uint payload = 0)
    {
        var sim = _simconnect;
        if (sim == null) return;
        // Hard write-guard — same as TransmitEventByName.
        if (!WriteEnabled)
        {
            _logger.LogInformation("Sim write skipped (Features.Sim.WriteEnabled = false): {Event} payload={Payload}", ev, payload);
            return;
        }

        sim.TransmitClientEvent(
            Microsoft.FlightSimulator.SimConnect.SimConnect.SIMCONNECT_OBJECT_ID_USER,
            ev,
            payload,
            NotificationGroup.Default,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
    }

    public void BeginDescent(int targetAltitudeFeet)
    {
        if (_simconnect == null)
        {
            _logger.LogWarning("BeginDescent skipped: SimConnect not connected.");
            return;
        }
        TransmitFcu("FCU_ALT_SET", (uint)targetAltitudeFeet);
        TransmitFcu("FCU_ALT_PUSH");
        _logger.LogInformation("BeginDescent target={Feet} ft", targetAltitudeFeet);
    }

    public void EngageAltitudeHold(int currentAltitudeFeet)
    {
        if (_simconnect == null)
        {
            _logger.LogWarning("EngageAltitudeHold skipped: SimConnect not connected.");
            return;
        }
        var rounded = (int)System.Math.Round(currentAltitudeFeet / 100.0) * 100;
        TransmitFcu("FCU_ALT_SET", (uint)rounded);
        TransmitFcu("FCU_ALT_PUSH");
        _logger.LogInformation("EngageAltitudeHold captured {Feet} ft", rounded);
    }

    public void EngageApproach()
    {
        if (_simconnect == null)
        {
            _logger.LogWarning("EngageApproach skipped: SimConnect not connected.");
            return;
        }
        TransmitFcu("FCU_APPR_PUSH");
        TransmitFcu("FCU_AP_1_PUSH");
        TransmitFcu("FCU_AP_2_PUSH");
        _logger.LogInformation("EngageApproach: APPR + AP1 + AP2");
    }

    // ---------- FCU panel verbs ----------

    public void FcuSetSpeed(int knots)          { if (_simconnect == null) return; TransmitFcu("FCU_SPD_SET", (uint)knots); }
    public void FcuPushSpeed()                  { if (_simconnect == null) return; TransmitFcu("FCU_SPD_PUSH"); }
    public void FcuPullSpeed()                  { if (_simconnect == null) return; TransmitFcu("FCU_SPD_PULL"); }

    public void FcuSetHeading(int degrees)      { if (_simconnect == null) return; TransmitFcu("FCU_HDG_SET", (uint)((degrees % 360 + 360) % 360)); }
    public void FcuPushHeading()                { if (_simconnect == null) return; TransmitFcu("FCU_HDG_PUSH"); }
    public void FcuPullHeading()                { if (_simconnect == null) return; TransmitFcu("FCU_HDG_PULL"); }

    public void FcuSetAltitude(int feet)        { if (_simconnect == null) return; var r = (int)System.Math.Round(feet / 100.0) * 100; TransmitFcu("FCU_ALT_SET", (uint)r); }
    public void FcuPushAltitude()               { if (_simconnect == null) return; TransmitFcu("FCU_ALT_PUSH"); }
    public void FcuPullAltitude()               { if (_simconnect == null) return; TransmitFcu("FCU_ALT_PULL"); }

    // V/S can be negative -- the WASM module reads the payload as a signed int, so we
    // pass the raw bits of the int as uint via unchecked cast.
    public void FcuSetVerticalSpeed(int fpm)    { if (_simconnect == null) return; TransmitFcu("FCU_VS_SET", unchecked((uint)fpm)); }
    public void FcuPushVerticalSpeed()          { if (_simconnect == null) return; TransmitFcu("FCU_VS_PUSH"); }
    public void FcuPullVerticalSpeed()          { if (_simconnect == null) return; TransmitFcu("FCU_VS_PULL"); }

    public void FcuToggleAp1()                  { if (_simconnect == null) return; TransmitFcu("FCU_AP_1_PUSH"); }
    public void FcuToggleAp2()                  { if (_simconnect == null) return; TransmitFcu("FCU_AP_2_PUSH"); }
    public void FcuToggleAthr()                 { if (_simconnect == null) return; TransmitFcu("FCU_ATHR_PUSH"); }
    public void FcuPushLoc()                    { if (_simconnect == null) return; TransmitFcu("FCU_LOC_PUSH"); }
    public void FcuPushAppr()                   { if (_simconnect == null) return; TransmitFcu("FCU_APPR_PUSH"); }
    public void FcuPushExped()                  { if (_simconnect == null) return; TransmitFcu("FCU_EXPED_PUSH"); }
}
