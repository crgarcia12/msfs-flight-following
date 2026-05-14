using Microsoft.Extensions.Logging;
using MSFSFlightFollowing.SimConnect;

namespace MSFSFlightFollowing.AgentsCore;

/// <summary>
/// Capability bundle handed to each agent. Keeps agents narrow: they get the bus
/// and a handful of well-typed simulator capabilities, nothing more.
///
/// <para>
/// <see cref="AgentsEnabled"/> is the master switch for all autonomous behaviour.
/// When <c>false</c>, agents should NOT subscribe to bus messages that would
/// cause them to write to the simulator or fire scripted callouts — they remain
/// resolvable so user-triggered methods (e.g. <c>RecoverAltitudeNow</c>,
/// <c>BeginDescentNow</c>) still work from the UI / REST endpoints.
/// </para>
/// </summary>
public sealed class AgentContext
{
    public IAgentBus Bus { get; }
    public ISimCommands Sim { get; }
    public ISimBridgeMcdu Mcdu { get; }
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Master switch for autonomous agent behaviour. When <c>false</c>, agents
    /// must not subscribe to bus messages that drive automatic actions; the app
    /// becomes a read-only display unless the user explicitly clicks something.
    /// </summary>
    public bool AgentsEnabled { get; }

    public AgentContext(IAgentBus bus, ISimCommands sim, ISimBridgeMcdu mcdu, ILoggerFactory loggerFactory, bool agentsEnabled)
    {
        Bus = bus;
        Sim = sim;
        Mcdu = mcdu;
        LoggerFactory = loggerFactory;
        AgentsEnabled = agentsEnabled;
    }
}
