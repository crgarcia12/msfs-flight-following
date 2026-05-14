using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSFSFlightFollowing.SimConnect;

namespace MSFSFlightFollowing.AgentsCore;

/// <summary>
/// Base class for cockpit agents. Subscribes to the bus through <see cref="Bus"/>
/// and publishes UI callouts via <see cref="SayAsync"/>.
/// </summary>
public abstract class AgentBase
{
    public string AgentName { get; }
    protected IAgentBus Bus { get; }
    protected ISimCommands Sim { get; }
    protected ISimBridgeMcdu Mcdu { get; }
    protected ILogger Logger { get; }

    protected AgentBase(AgentContext ctx, string agentName)
    {
        AgentName = agentName;
        Bus = ctx.Bus;
        Sim = ctx.Sim;
        Mcdu = ctx.Mcdu;
        Logger = ctx.LoggerFactory.CreateLogger($"Agents.{agentName}");
    }

    /// <summary>
    /// Publish a checklist callout that shows on the UI. If <paramref name="pilotResponse"/>
    /// is non-empty, the Pilot agent will echo it after ~1 s.
    /// </summary>
    protected Task SayAsync(string message, string pilotResponse = "")
        => Bus.PublishAsync(new ChecklistCallout(AgentName, message, pilotResponse));
}
