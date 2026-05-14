using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MSFSFlightFollowing.AgentsCore;
using MSFSFlightFollowing.Models;

namespace MSFSFlightFollowing.Runtime;

/// <summary>
/// Bridges <see cref="ChecklistCallout"/> bus messages to the browser as
/// SignalR <c>ReceiveAgentEvent</c> frames. This is the single place where the
/// agent layer touches SignalR — agents stay free of transport concerns.
/// </summary>
public sealed class SignalRAgentBridge
{
    private readonly IHubContext<WebSocketConnector> _hub;
    private readonly ILogger<SignalRAgentBridge> _logger;

    public SignalRAgentBridge(IAgentBus bus, IHubContext<WebSocketConnector> hub, ILogger<SignalRAgentBridge> logger)
    {
        _hub = hub;
        _logger = logger;
        bus.Subscribe<ChecklistCallout>(OnCallout);
    }

    private async Task OnCallout(ChecklistCallout callout)
    {
        var payload = new AgentFrontEndEvent
        {
            agent = callout.Agent,
            message = callout.Message
        };
        try
        {
            await _hub.Clients.All.SendAsync("ReceiveAgentEvent", payload);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "SignalR ReceiveAgentEvent send failed");
        }
    }
}
