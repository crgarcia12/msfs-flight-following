using System.Threading.Tasks;
using MSFSFlightFollowing.AgentsCore;

namespace MSFSFlightFollowing.Agents;

/// <summary>
/// Hands the cockpit checklist for the demo flight:
/// <list type="bullet">
///   <item>Takeoff clearance once we start rolling.</item>
///   <item>Lights/autopilot callouts at 3 000 ft and 10 000 ft (both directions).</item>
///   <item>Descent + MCDU + auto-land script when an approach is cleared.</item>
/// </list>
/// All thresholds are detected upstream and arrive as typed messages — the agent
/// itself holds no altitude state.
/// </summary>
public sealed class Copilot : AgentBase
{
    private const int DivertTargetAltitudeFt = 4_000;

    public Copilot(AgentContext ctx) : base(ctx, nameof(Copilot))
    {
        if (!ctx.AgentsEnabled) return;
        Bus.Subscribe<TakeoffStarted>(OnTakeoff);
        Bus.Subscribe<AltitudeCallout>(OnAltitudeCallout);
        Bus.Subscribe<ApproachCleared>(OnApproachCleared);
    }

    private Task OnTakeoff(TakeoffStarted _)
        => SayAsync("Ready for TakeOff", pilotResponse: "Take Off approved");

    private Task OnAltitudeCallout(AltitudeCallout c) => (c.Feet, c.Ascending) switch
    {
        (10_000, true)  => SayAsync("Turn lights off",          pilotResponse: "Landing lights off"),
        (10_000, false) => SayAsync("Turn landing lights on",   pilotResponse: "Landing lights on"),
        ( 3_000, true)  => SayAsync("Turn on Autopilot",        pilotResponse: "Autopilot ON"),
        ( 3_000, false) => OnPassing3000Descending(),
        _ => Task.CompletedTask
    };

    private async Task OnPassing3000Descending()
    {
        await SayAsync("Set landing AP", pilotResponse: "AP auto-landing on");
        Sim.EngageApproach();
    }

    private async Task OnApproachCleared(ApproachCleared msg)
    {
        await SayAsync(
            $"Initiate descent for Runway {msg.Runway}",
            pilotResponse: $"Initiating descent to {DivertTargetAltitudeFt} feet");
        Sim.BeginDescent(DivertTargetAltitudeFt);

        await SayAsync("Start configuring Autopilot Computer", pilotResponse: "Configuring Autopilot computer");
        await Mcdu.ChangeAirportAsync();

        await SayAsync("Computer configured", pilotResponse: "Check computer configured");
    }
}
