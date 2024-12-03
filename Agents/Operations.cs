using System.Threading.Tasks;

namespace MSFSFlightFollowing;

public class Operations : AgentBase
{
    public Operations(AgentManager agentManager) : base(agentManager, nameof(Operations))
    {
    }

    public override async Task ProcessEvent(AgentEvent agentEvent)
    {
    }
}
