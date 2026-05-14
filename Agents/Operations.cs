using System.Threading.Tasks;
using MSFSFlightFollowing.AgentsCore;

namespace MSFSFlightFollowing.Agents;

/// <summary>
/// Reacts to an <see cref="AtcMessage"/> by checking alternates and assigning a
/// new destination on the bus.
/// </summary>
public sealed class Operations : AgentBase
{
    public Operations(AgentContext ctx) : base(ctx, nameof(Operations))
    {
        if (!ctx.AgentsEnabled) return;
        Bus.Subscribe<AtcMessage>(OnAtc);
    }

    private async Task OnAtc(AtcMessage msg)
    {
        await SayAsync("New Flight Plan required. Checking alternates");
        await Task.Delay(1000);
        await Bus.PublishAsync(new DestinationAssigned("LSZH"));
        await SayAsync("New Destination: ZURICH");
    }
}
