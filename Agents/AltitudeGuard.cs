using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MSFSFlightFollowing.AgentsCore;
using MSFSFlightFollowing.SimConnect;

namespace MSFSFlightFollowing.Agents;

/// <summary>
/// Watches the A32NX FMA vertical mode. SRS (Speed Reference System) is a takeoff/go-around
/// pitch mode — it's expected during FMGC phases TAKEOFF (1) and GO_AROUND (6).
///
/// Outside those phases SRS almost always means TOGA was pushed by accident (e.g. you
/// nudged the thrust levers past CLB). The aircraft will pitch up and chase V2+10 instead
/// of holding altitude, which is a bad surprise mid-flight.
///
/// This agent:
/// <list type="bullet">
///   <item>Annunciates the FMA mode change.</item>
///   <item>If SRS persists outside takeoff/GA for &gt; 5 s, fires a callout asking the pilot
///         to hit RECOVER ALT.</item>
///   <item>Exposes <see cref="RecoverAltitudeNow"/> for the UI button / API endpoint.</item>
/// </list>
/// </summary>
public sealed class AltitudeGuard : AgentBase
{
    private const int FMGC_TAKEOFF = 1;
    private const int FMGC_GO_AROUND = 6;
    private static readonly TimeSpan SrsAlertAfter = TimeSpan.FromSeconds(5);

    private Stopwatch? _srsUnexpectedSince;
    private bool _alertedForCurrentSrs;
    private int _lastVerticalMode;
    private int _lastLastAltitudeFt;

    public AltitudeGuard(AgentContext ctx) : base(ctx, nameof(AltitudeGuard))
    {
        // Autonomous SRS detection only runs when agents are enabled. The
        // RecoverAltitudeNow() method below is always callable from the UI.
        if (!ctx.AgentsEnabled) return;
        Bus.Subscribe<AircraftSnapshot>(OnSnapshotAsync);
        Bus.Subscribe<SimDisconnected>(_ =>
        {
            ResetState();
            return Task.CompletedTask;
        });
    }

    private async Task OnSnapshotAsync(AircraftSnapshot snap)
    {
        var ac = snap.Aircraft;
        _lastLastAltitudeFt = (int)Math.Round(ac.Altitude);

        // No FBW A32NX loaded? Nothing to guard.
        if (!ac.IsA32nx)
        {
            ResetState();
            return;
        }

        var vMode = ac.A32nxFmaVerticalMode;

        // Mode change announcement (only when SRS engages/disengages).
        if (vMode != _lastVerticalMode)
        {
            if (A32nxFmaModes.IsSrs(vMode))
            {
                await SayAsync($"FMA: {ac.FmaPitch} / {ac.FmaRoll} engaged");
            }
            else if (A32nxFmaModes.IsSrs(_lastVerticalMode))
            {
                await SayAsync($"FMA: SRS cleared → {ac.FmaPitch}");
                _alertedForCurrentSrs = false;
                _srsUnexpectedSince = null;
            }
            _lastVerticalMode = vMode;
        }

        // Not in SRS at all → nothing to do.
        if (!A32nxFmaModes.IsSrs(vMode))
        {
            _srsUnexpectedSince = null;
            _alertedForCurrentSrs = false;
            return;
        }

        // SRS is normal during takeoff / go-around. Below 100 ft RA on the ground we're
        // pre-takeoff (FMA already in SRS, no surprise).
        var expected =
            ac.A32nxFmgcFlightPhase == FMGC_TAKEOFF ||
            ac.A32nxFmgcFlightPhase == FMGC_GO_AROUND ||
            ac.OnGround;

        if (expected)
        {
            _srsUnexpectedSince = null;
            _alertedForCurrentSrs = false;
            return;
        }

        _srsUnexpectedSince ??= Stopwatch.StartNew();
        if (_alertedForCurrentSrs || _srsUnexpectedSince.Elapsed < SrsAlertAfter) return;

        _alertedForCurrentSrs = true;
        await Bus.PublishAsync(new UnexpectedSrsDetected((int)Math.Round(ac.Altitude), ac.A32nxFmgcFlightPhase));
        await SayAsync(
            $"Unexpected SRS at {(int)Math.Round(ac.Altitude)} ft. Press RECOVER ALT to hold current altitude.");
    }

    /// <summary>
    /// Public API used by the HTTP endpoint / UI button. Captures the current altitude
    /// (rounded to the nearest 100 ft) on the FCU and engages ALT hold.
    /// </summary>
    public async Task RecoverAltitudeNow()
    {
        var alt = _lastLastAltitudeFt;
        if (alt <= 0)
        {
            await SayAsync("Cannot recover: no aircraft data available.");
            return;
        }
        Sim.EngageAltitudeHold(alt);
        var rounded = (int)Math.Round(alt / 100.0) * 100;
        await SayAsync($"Recover ALT: capturing {rounded} ft.");
    }

    private void ResetState()
    {
        _srsUnexpectedSince = null;
        _alertedForCurrentSrs = false;
        _lastVerticalMode = 0;
    }
}
