using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSFSFlightFollowing.AgentsCore;
using MSFSFlightFollowing.Models;

namespace MSFSFlightFollowing;

public class SimBridgeClient : ISimBridgeMcdu
{
    private readonly FeatureOptions _features;
    private readonly ILogger<SimBridgeClient> _logger;

    public bool Enabled => _features.SimBridge.Enabled;
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Connection state surfaced to the UI so it can show a precise hint:
    /// <list type="bullet">
    ///   <item><c>Disabled</c> — feature flag is off.</item>
    ///   <item><c>Connecting</c> — feature on, WebSocket not yet established.</item>
    ///   <item><c>AwaitingAircraft</c> — WebSocket up, but no MCDU frame has arrived
    ///     (FBW / Headwind aircraft not loaded, or <c>CONFIG_SIMBRIDGE_ENABLED</c>
    ///     persistent setting is off in the cockpit MCDU).</item>
    ///   <item><c>Streaming</c> — at least one MCDU frame received.</item>
    /// </list>
    /// </summary>
    public SimBridgeStatus Status { get; private set; } = SimBridgeStatus.Disabled;

    /// <summary>
    /// Latest MCDU snapshot from FlyByWire SimBridge, including FBW color tags
    /// (<c>{white}</c>, <c>{cyan}</c>, <c>{green}</c>, <c>{amber}</c>, <c>{magenta}</c>,
    /// <c>{small}</c>, <c>{big}</c>, <c>{sp}</c>, <c>{end}</c>) so the front-end can
    /// render the display in colour. Returns <c>null</c> if not connected.
    /// </summary>
    public FmcRoot? CurrentScreen
    {
        get
        {
            lock (fmcRootLock) { return _root; }
        }
    }

    public SimBridgeClient(IOptions<FeatureOptions> features, ILogger<SimBridgeClient> logger)
    {
        _features = features.Value;
        _logger = logger;
    }

    object fmcRootLock = new object();

    FmcRoot _root = null;
    FmcRoot fmcRootDataObject
    {
        get
        {
            lock (fmcRootLock)
            {
                return _root;
            }
        }
        set
        {
            lock (fmcRootLock)
            {
                _root = value;
            }
            fmcVersion++;
        }
    }
    int fmcVersion = 0;

    ClientWebSocket ws;

    /// <summary>
    /// Strips the FBW MCDU color/format tags from a string so it can be matched
    /// against plain text (used by the automation methods). The wire payload sent
    /// to the front-end keeps the tags so it can render in colour.
    /// </summary>
    public static string StripTags(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        return s
            .Replace("{end}", "")
            .Replace("{white}", "")
            .Replace("{cyan}", "")
            .Replace("{green}", "")
            .Replace("{amber}", "")
            .Replace("{magenta}", "")
            .Replace("{small}", "")
            .Replace("{big}", "")
            .Replace("{sp}", " ")
            .Replace("{right}", "")
            .Replace("{left}", "");
    }

    private async Task SendRequestUpdate(ClientWebSocket ws)
    {
        ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes("requestUpdate"));
        await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);

        await Task.Delay(500);
    }

    // Every time we press something opn the FMC, the server sends and update (so it is not needed to request an update)
    private async Task ReceiveMessagesAndUpdateRoot(ClientWebSocket ws, bool sendRequestUpdate = true)
    {
        while (true)
        {
            // Request update
            if (sendRequestUpdate)
            {
                await Task.Delay(500);
                sendRequestUpdate = false;
            }

            // Receive a message from the server
            var bytesReceived = new ArraySegment<byte>(new byte[40960]);
            WebSocketReceiveResult result = await ws.ReceiveAsync(bytesReceived, CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogInformation("SimBridge closed the connection.");
                return;
            }

            string receivedMessage = Encoding.UTF8.GetString(bytesReceived.Array!, 0, result.Count);
            if (string.IsNullOrEmpty(receivedMessage)) continue;

            // The SimBridge MCDU gateway broadcasts *every* message to every connected
            // client, so we see a mix of:
            //   - "mcduConnected"            (aircraft → server handshake echoed back)
            //   - "requestUpdate"            (another client polling)
            //   - "event:left:<KEY>" / "event:right:<KEY>"  (our or another panel's keypresses)
            //   - "print:<json>"             (printer payloads)
            //   - "update:<json>"            (the actual MCDU frame — what we want)
            // Anything that isn't an update frame is ignored to avoid JSON parse errors
            // when the aircraft (or another client) sends control messages.
            if (!receivedMessage.StartsWith("update:", StringComparison.Ordinal))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var preview = receivedMessage.Length > 80 ? receivedMessage.Substring(0, 80) + "..." : receivedMessage;
                    _logger.LogDebug("SimBridge non-update message ignored: {Msg}", preview);
                }
                continue;
            }

            try
            {
                string json = receivedMessage.Substring(7);
                FmcRoot? fmcRootDTO = JsonSerializer.Deserialize<FmcRoot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (fmcRootDTO == null) continue;

                bool first = fmcRootDataObject == null;
                fmcRootDataObject = fmcRootDTO;
                if (first)
                {
                    Status = SimBridgeStatus.Streaming;
                    _logger.LogInformation("SimBridge MCDU stream started (first frame received).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SimBridge update parse failed (ignored).");
            }
        }
    }

    private async Task Press(ClientWebSocket ws, string button)
    {
        int currentVersion = fmcVersion;

        string message = $"event:left:{button}";
        ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
        await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);

        while (currentVersion == fmcVersion)
        {
            await Task.Delay(500);
        }
    }

    // Whitelist of MCDU key codes accepted by the FBW SimBridge gateway. The
    // names must match exactly what the Remote-MCDU client emits over its
    // "event:<side>:<KEY>" protocol — see /interfaces/mcdu/index.js inside
    // SimBridge. Anything not in this set is rejected so a malicious or buggy
    // client can't smuggle arbitrary payloads to the cockpit.
    private static readonly System.Collections.Generic.HashSet<string> McduKeyWhitelist = new(StringComparer.Ordinal)
    {
        "L1","L2","L3","L4","L5","L6",
        "R1","R2","R3","R4","R5","R6",
        "DIR","PROG","PERF","INIT","DATA",
        "FPLN","RAD","FUEL","MENU","AIRPORT",
        "PREVPAGE","NEXTPAGE","UP","DOWN",
        "CLR","OVFY","DIV","SP","PLUSMINUS","DOT","BRT","DIM",
        "A","B","C","D","E","F","G","H","I","J","K","L","M",
        "N","O","P","Q","R","S","T","U","V","W","X","Y","Z",
        "0","1","2","3","4","5","6","7","8","9"
    };

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    /// <summary>
    /// Fires a single MCDU keypress over the existing SimBridge WebSocket.
    /// <paramref name="side"/> selects captain ("left") or first-officer ("right").
    /// <paramref name="key"/> must be a member of <see cref="McduKeyWhitelist"/>.
    /// </summary>
    public async Task<bool> SendKeyAsync(string side, string key)
    {
        if (!Enabled || !IsConnected || ws == null) return false;
        if (string.IsNullOrWhiteSpace(key)) return false;

        var normalized = key.Trim().ToUpperInvariant();
        if (!McduKeyWhitelist.Contains(normalized))
        {
            _logger.LogWarning("MCDU key rejected (not in whitelist): {Key}", key);
            return false;
        }

        var s = string.Equals(side, "right", StringComparison.OrdinalIgnoreCase) ? "right" : "left";
        var bytes = Encoding.UTF8.GetBytes($"event:{s}:{normalized}");

        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("MCDU key send failed ({Side}:{Key}): {Message}", s, normalized, ex.Message);
            return false;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task Type(ClientWebSocket ws, string text)
    {
        foreach (char character in text)
        {
            string button = character.ToString();
            if (button == "/")
            {
                button = "DIV";
            }

            await Press(ws, button);
        }

    }

    public async Task ChangeAirportAsync() => await ChangeAirport();

    public async Task ChangeAirport()
    {
        if (!Enabled || !IsConnected || ws == null || fmcRootDataObject == null)
        {
            _logger.LogInformation("SimBridge ChangeAirport skipped (Enabled={Enabled}, Connected={Connected}).", Enabled, IsConnected);
            return;
        }

        while (!string.IsNullOrEmpty(StripTags(fmcRootDataObject.Left.Scratchpad)))
        {
            await Press(ws, "CLR");
            await Task.Delay(1000);
        }

        // Changin INIT page to new destination
        await Press(ws, "INIT");
        await Type(ws, "LSME/LSZH");
        await Task.Delay(500);
        await Press(ws, "R1");
        await Task.Delay(1000);
        await Press(ws, "L6");
        await Task.Delay(500);

        // Configure Flight plan
        await Press(ws, "FPLN");
        await Task.Delay(1000);
        await Press(ws, "L6"); //LSZH
        await Press(ws, "R1"); //Arrival
        await Press(ws, "L5"); //ILS28
        await Press(ws, "L5"); //ILS28
        await Press(ws, "R6"); //INSERT

        // Direct
        await Press(ws, "DIR");

        bool found = false;
        while (!found)
        {
            int index = 0;
            foreach (List<string> line in fmcRootDataObject.Left.Lines)
            {
                // There are 3 columns in every row. I am interested in the first one (0)
                if (!found && StripTags(line[0]).Contains("CF28"))
                {
                    found = true;
                    int buttonIndex = (index + 1) / 2;
                    await Press(ws, $"L{buttonIndex.ToString()}");
                    await Press(ws, $"R6"); //*Direct
                }
                index++;
            }
            if (!found)
            {
                await Press(ws, "UP");
                await Task.Delay(1000);
            }
        }

        // "scratchpad": "{white}T/D REACHED{end}",
    }

    public async Task Connect()
    {
        if (!Enabled)
        {
            Status = SimBridgeStatus.Disabled;
            _logger.LogInformation("SimBridge is disabled in configuration; skipping connection.");
            return;
        }

        Status = SimBridgeStatus.Connecting;

        try
        {
            ws = new ClientWebSocket();
            Uri serverUri = new Uri(_features.SimBridge.Url);
            await ws.ConnectAsync(serverUri, CancellationToken.None);
            await SendRequestUpdate(ws);
            IsConnected = true;
            // Until we see an update: frame we treat the aircraft as missing; the
            // UI uses this to show a "waiting for aircraft" hint instead of a hard
            // "SimBridge offline" error.
            Status = SimBridgeStatus.AwaitingAircraft;
            _logger.LogInformation("SimBridge connected ({Url}). Waiting for first MCDU frame...", _features.SimBridge.Url);

            _ = Task.Run(async () =>
            {
                try { await ReceiveMessagesAndUpdateRoot(ws); }
                catch (Exception ex)
                {
                    _logger.LogWarning("SimBridge receive loop ended: {Message}", ex.Message);
                }
                finally
                {
                    IsConnected = false;
                    Status = SimBridgeStatus.Connecting;
                    lock (fmcRootLock) { _root = null; }
                }
            });
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Status = SimBridgeStatus.Connecting;
            _logger.LogWarning("SimBridge connection failed ({Url}): {Message}", _features.SimBridge.Url, ex.Message);
        }
    }
}

/// <summary>
/// Coarse-grained SimBridge state, sent to the browser so the offline UI can
/// give the user actionable guidance.
/// </summary>
public enum SimBridgeStatus
{
    Disabled = 0,
    Connecting = 1,
    AwaitingAircraft = 2,
    Streaming = 3
}
