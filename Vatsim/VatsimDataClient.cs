using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSFSFlightFollowing.Models;

namespace MSFSFlightFollowing.Vatsim;

/// <summary>
/// Thin HTTP poller for the two public VATSIM data feeds. No side effects; it
/// just returns the parsed payloads. Errors are logged and turned into <c>null</c>
/// so that callers can keep running and try again on the next tick.
/// </summary>
public sealed class VatsimDataClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly FeatureOptions.VatsimOptions _opts;
    private readonly ILogger<VatsimDataClient> _logger;

    public VatsimDataClient(IOptions<FeatureOptions> opts, ILogger<VatsimDataClient> logger)
    {
        _opts = opts.Value.Vatsim;
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MSFSFlightFollowing/1.0 (+github.com/crgarcia12/msfs-flight-following)");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    internal async Task<VatsimDataDto?> GetDataAsync(CancellationToken ct)
    {
        try
        {
            return await _http.GetFromJsonAsync<VatsimDataDto>(_opts.DataUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "VATSIM data fetch failed");
            return null;
        }
    }

    internal async Task<List<VatsimTransceiverGroupDto>?> GetTransceiversAsync(CancellationToken ct)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<VatsimTransceiverGroupDto>>(_opts.TransceiversUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "VATSIM transceivers fetch failed");
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
