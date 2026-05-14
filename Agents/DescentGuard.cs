using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSFSFlightFollowing.AgentsCore;
using MSFSFlightFollowing.Models;
using MSFSFlightFollowing.SimConnect;

namespace MSFSFlightFollowing.Agents;

/// <summary>
/// Auto-descent guard for the FlyByWire A32NX and Headwind A330 (A339X).
///
/// Both aircraft raise <c>L:A32NX_PFD_MSG_TD_REACHED</c> when their VNAV component
/// detects the aircraft has crossed top-of-descent and no descent has been armed
/// yet. This agent watches the rising edge of that LVar and, on each fire,
/// performs the standard Airbus "begin descent" gesture:
/// <list type="number">
///   <item>Dial the FCU ALT down to <see cref="FeatureOptions.DescentOptions.TargetAltitudeFeet"/>.</item>
///   <item>Push the ALT knob (managed descent — follow the FMS profile).</item>
/// </list>
///
/// The FCU events are emitted as both <c>A32NX.FCU_*</c> and <c>A339X.FCU_*</c>,
/// so the same fire works on the A320 and the A330 transparently.
///
/// A cooldown prevents the agent from re-firing if the LVar toggles rapidly
/// (which can happen on long step descents or after a hold).
/// </summary>
public sealed class DescentGuard : AgentBase
{
    private readonly FeatureOptions.DescentOptions _opts;

    private bool _lastTdReached;
    private DateTimeOffset _lastFireAt = DateTimeOffset.MinValue;

    public DescentGuard(AgentContext ctx, IOptions<FeatureOptions> opts) : base(ctx, nameof(DescentGuard))
    {
        _opts = opts.Value.Descent;
        // Autonomous T/D detection only runs when agents are enabled AND the
        // descent feature itself is on. BeginDescentNow() remains callable from
        // the UI regardless.
        if (!ctx.AgentsEnabled || !_opts.Enabled) return;
        Bus.Subscribe<AircraftSnapshot>(OnSnapshotAsync);
        Bus.Subscribe<SimDisconnected>(_ =>
        {
            _lastTdReached = false;
            _lastFireAt = DateTimeOffset.MinValue;
            return Task.CompletedTask;
        });
    }

    private async Task OnSnapshotAsync(AircraftSnapshot snap)
    {
        var ac = snap.Aircraft;
        if (!ac.IsA32nx) return;

        var td = ac.A32nxTdReached;
        var rising = td && !_lastTdReached;
        _lastTdReached = td;

        if (!rising) return;
        if (!_opts.Enabled)
        {
            await SayAsync("T/D reached — auto-descent disabled in settings.");
            return;
        }

        var cooldown = TimeSpan.FromSeconds(Math.Max(0, _opts.CooldownSeconds));
        if (DateTimeOffset.UtcNow - _lastFireAt < cooldown)
        {
            Logger.LogInformation("T/D rising edge ignored: still in cooldown.");
            return;
        }

        await BeginDescentNow(_opts.TargetAltitudeFeet);
    }

    /// <summary>
    /// Public entry point for the manual UI button / API endpoint. Sets the FCU ALT
    /// to <paramref name="targetFeet"/> (or the configured default) and pushes ALT.
    /// </summary>
    public async Task BeginDescentNow(int? targetFeet = null)
    {
        var target = targetFeet ?? _opts.TargetAltitudeFeet;
        // Clamp to a sane range — the FCU itself rejects values outside this.
        target = Math.Clamp(target, 100, 49000);

        _lastFireAt = DateTimeOffset.UtcNow;
        Sim.BeginDescent(target);
        Logger.LogInformation("DescentGuard: armed managed descent to {Feet} ft", target);
        await SayAsync($"T/D reached. Descending to {target:N0} ft.");
    }
}
