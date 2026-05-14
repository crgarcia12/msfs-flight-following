using System.Threading.Tasks;
using MSFSFlightFollowing.AgentsCore;

namespace MSFSFlightFollowing.Agents;

/// <summary>
/// Plays the role of the human pilot: when a <see cref="ChecklistCallout"/>
/// includes an expected pilot response, the Pilot echoes it ~1 s later so the
/// timeline reads as a real back-and-forth.
/// The 1 s delay runs fire-and-forget so it never blocks bus delivery.
/// </summary>
public sealed class Pilot : AgentBase
{
    private const int EchoDelayMs = 1000;

    public Pilot(AgentContext ctx) : base(ctx, nameof(Pilot))
    {
        if (!ctx.AgentsEnabled) return;
        Bus.Subscribe<ChecklistCallout>(OnCallout);
    }

    private Task OnCallout(ChecklistCallout callout)
    {
        // Ignore my own echoes and callouts that don't request a response.
        if (callout.Agent == AgentName) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(callout.PilotResponse)) return Task.CompletedTask;

        var response = callout.PilotResponse;
        _ = Task.Run(async () =>
        {
            await Task.Delay(EchoDelayMs);
            await SayAsync(response);
        });
        return Task.CompletedTask;
    }
}
