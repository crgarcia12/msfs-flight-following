using System.Threading.Tasks;

namespace MSFSFlightFollowing;

public abstract class AgentBase
{
    internal AgentManager _agentManager;
    // used by the front end
    public readonly string AgentName;

    public AgentBase(AgentManager agentManager, string agentName)
    {
        _agentManager = agentManager;
        AgentName = agentName;
    }

    public abstract Task ProcessEvent(AgentEvent agentEvent);
}
