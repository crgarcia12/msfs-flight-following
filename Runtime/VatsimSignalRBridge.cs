using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MSFSFlightFollowing.AgentsCore;
using MSFSFlightFollowing.Models;

namespace MSFSFlightFollowing.Runtime;

/// <summary>
/// Forwards VATSIM bus events to the browser as the <c>ReceiveVatsim</c> SignalR
/// event. The payload is the full list of nearby controllers so the front-end
/// can render the panel directly without keeping its own state machine.
/// </summary>
public sealed class VatsimSignalRBridge
{
    private readonly IHubContext<WebSocketConnector> _hub;
    private readonly ILogger<VatsimSignalRBridge> _logger;

    public VatsimSignalRBridge(IAgentBus bus, IHubContext<WebSocketConnector> hub, ILogger<VatsimSignalRBridge> logger)
    {
        _hub = hub;
        _logger = logger;
        bus.Subscribe<NearbyControllersChanged>(OnChanged);
        bus.Subscribe<VatsimRefreshed>(OnRefreshed);
    }

    private Task OnChanged(NearbyControllersChanged msg) => SendAsync(msg.Controllers);
    private Task OnRefreshed(VatsimRefreshed msg)        => SendAsync(msg.Controllers);

    private async Task SendAsync(System.Collections.Generic.IReadOnlyList<Vatsim.NearbyController> controllers)
    {
        try
        {
            await _hub.Clients.All.SendAsync("ReceiveVatsim", new { controllers });
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "SignalR ReceiveVatsim send failed");
        }
    }
}
