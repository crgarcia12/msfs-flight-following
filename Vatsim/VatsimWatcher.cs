using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSFSFlightFollowing.AgentsCore;
using MSFSFlightFollowing.Models;

namespace MSFSFlightFollowing.Vatsim;

/// <summary>
/// Owns the VATSIM polling loop. On each tick it fetches both feeds,
/// matches against the most recent aircraft position, and publishes:
/// <list type="bullet">
///   <item><see cref="NearbyControllersChanged"/> whenever the in-range set changes.</item>
///   <item><see cref="VatsimRefreshed"/> on every other tick so distances stay live.</item>
///   <item><see cref="AtisUpdated"/> whenever an ATIS station's info letter changes.</item>
/// </list>
/// When <c>Features.Vatsim.Enabled = false</c> the service self-disables.
/// </summary>
public sealed class VatsimWatcher : BackgroundService
{
    private readonly IAgentBus _bus;
    private readonly VatsimDataClient _client;
    private readonly FeatureOptions.VatsimOptions _opts;
    private readonly ILogger<VatsimWatcher> _logger;

    private double _aircraftLat;
    private double _aircraftLon;
    private bool _hasAircraftPosition;

    private HashSet<string> _lastInRangeCallsigns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lastAtisLetter = new(StringComparer.OrdinalIgnoreCase);

    public VatsimWatcher(
        IAgentBus bus,
        VatsimDataClient client,
        IOptions<FeatureOptions> opts,
        ILogger<VatsimWatcher> logger)
    {
        _bus = bus;
        _client = client;
        _opts = opts.Value.Vatsim;
        _logger = logger;
        _bus.Subscribe<AircraftSnapshot>(OnSnapshot);
        _bus.Subscribe<SimDisconnected>(OnDisconnected);
    }

    private Task OnSnapshot(AircraftSnapshot snap)
    {
        _aircraftLat = snap.Aircraft.Latitude;
        _aircraftLon = snap.Aircraft.Longitude;
        _hasAircraftPosition = true;
        return Task.CompletedTask;
    }

    private Task OnDisconnected(SimDisconnected _)
    {
        _hasAircraftPosition = false;
        _lastInRangeCallsigns.Clear();
        _lastAtisLetter.Clear();
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.Enabled)
        {
            _logger.LogInformation("VATSIM integration disabled in configuration.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(15, _opts.PollIntervalSeconds));
        _logger.LogInformation("VATSIM watcher started. Polling every {Sec}s", interval.TotalSeconds);

        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VATSIM poll iteration failed");
            }

            try { await Task.Delay(interval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        if (!_hasAircraftPosition) return;

        var dataTask = _client.GetDataAsync(ct);
        var txTask = _client.GetTransceiversAsync(ct);
        await Task.WhenAll(dataTask, txTask).ConfigureAwait(false);

        var data = dataTask.Result;
        var tx   = txTask.Result;
        if (data is null || tx is null) return;

        var positions = VatsimMatcher.BuildStationPositions(tx);
        var nearby = VatsimMatcher.Match(data, positions, _aircraftLat, _aircraftLon, _opts.RangeBufferNm);

        var nowSet = new HashSet<string>(nearby.Select(n => n.Callsign), StringComparer.OrdinalIgnoreCase);
        if (!nowSet.SetEquals(_lastInRangeCallsigns))
        {
            var entered = nearby.Where(n => !_lastInRangeCallsigns.Contains(n.Callsign)).ToList();
            var left    = _lastInRangeCallsigns.Where(c => !nowSet.Contains(c)).ToList();

            _lastInRangeCallsigns = nowSet;
            _logger.LogInformation("VATSIM: {Count} controllers in range (+{Entered}/-{Left})",
                nearby.Count, entered.Count, left.Count);
            await _bus.PublishAsync(new NearbyControllersChanged(nearby, entered, left)).ConfigureAwait(false);
        }
        else
        {
            await _bus.PublishAsync(new VatsimRefreshed(nearby)).ConfigureAwait(false);
        }

        foreach (var atis in nearby.Where(n => n.FacilityShort == "ATIS" && n.AtisCode is not null))
        {
            if (!_lastAtisLetter.TryGetValue(atis.Callsign, out var prev) || prev != atis.AtisCode)
            {
                _lastAtisLetter[atis.Callsign] = atis.AtisCode!;
                await _bus.PublishAsync(new AtisUpdated(atis.Callsign, atis.AtisCode!, atis.AtisText ?? "")).ConfigureAwait(false);
            }
        }
    }
}
