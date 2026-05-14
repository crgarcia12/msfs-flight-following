using System.Threading.Tasks;
using MSFSFlightFollowing.AgentsCore;

namespace MSFSFlightFollowing.Agents;

/// <summary>
/// Translates a new destination into a concrete approach clearance for the copilot.
/// </summary>
public sealed class Navigator : AgentBase
{
    public Navigator(AgentContext ctx) : base(ctx, nameof(Navigator))
    {
        if (!ctx.AgentsEnabled) return;
        Bus.Subscribe<DestinationAssigned>(OnDestination);
    }

    private async Task OnDestination(DestinationAssigned msg)
    {
        await SayAsync($"New Destination: {msg.Icao} — Recommend Runway 28");
        await Bus.PublishAsync(new ApproachCleared(msg.Icao, "28"));
    }
}
