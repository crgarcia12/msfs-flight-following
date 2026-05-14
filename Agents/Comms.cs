using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MSFSFlightFollowing.AgentsCore;

namespace MSFSFlightFollowing.Agents;

/// <summary>
/// Communications agent. Owns two roles:
/// <list type="bullet">
///   <item>The scripted "Stuttgart is closed" demo trigger (above 7 000 ft for 5 s).</item>
///   <item>VATSIM awareness — announces when a new controller comes into range,
///         and reads back the destination ATIS letter whenever it updates.</item>
/// </list>
/// </summary>
public sealed class Comms : AgentBase
{
    private const double TriggerAltitudeFt = 7_000;
    private static readonly TimeSpan StableFor = TimeSpan.FromSeconds(5);

    private IDisposable? _snapshotSub;
    private Stopwatch? _stableSince;
    private bool _firedScenario;

    public Comms(AgentContext ctx) : base(ctx, nameof(Comms))
    {
        if (!ctx.AgentsEnabled) return;
        _snapshotSub = Bus.Subscribe<AircraftSnapshot>(OnSnapshotAsync);
        Bus.Subscribe<NearbyControllersChanged>(OnVatsimChangedAsync);
        Bus.Subscribe<AtisUpdated>(OnAtisUpdatedAsync);
    }

    private async Task OnSnapshotAsync(AircraftSnapshot snap)
    {
        if (_firedScenario) return;

        if (snap.Aircraft.Altitude < TriggerAltitudeFt)
        {
            _stableSince = null;
            return;
        }

        _stableSince ??= Stopwatch.StartNew();
        if (_stableSince.Elapsed < StableFor) return;

        _firedScenario = true;
        _snapshotSub?.Dispose();
        _snapshotSub = null;

        await Bus.PublishAsync(new AtcMessage("ATC: Stuttgart airport is closed due to bad weather!"));
        await SayAsync("Validate new route", pilotResponse: "Landing at Zurich");
    }

    private async Task OnVatsimChangedAsync(NearbyControllersChanged msg)
    {
        // Voice-style callouts for every controller that just came into range —
        // skip ATIS stations because we'll read those separately on the AtisUpdated event.
        foreach (var c in msg.Entered)
        {
            if (string.Equals(c.FacilityShort, "ATIS", StringComparison.OrdinalIgnoreCase))
                continue;

            await SayAsync($"{c.Callsign} ({c.FacilityShort}) in range on {c.Frequency} — {c.DistanceNm:0.#} NM");
        }
    }

    private Task OnAtisUpdatedAsync(AtisUpdated msg)
    {
        // Read the first non-empty line of the ATIS as a quick callout.
        // The full text is available on the UI panel for the pilot to read at leisure.
        var firstLine = "";
        foreach (var line in msg.Text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0) { firstLine = trimmed; break; }
        }

        var summary = string.IsNullOrEmpty(firstLine)
            ? $"{msg.Callsign} information {msg.AtisCode} now current"
            : $"{msg.Callsign} information {msg.AtisCode}: {firstLine}";
        return SayAsync(summary);
    }
}
