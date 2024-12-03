using System.Threading.Tasks;

namespace MSFSFlightFollowing;

public class Operations : AgentBase
{
    public Operations(AgentManager agentManager) : base(agentManager, nameof(Operations))
    {
    }

    public override async Task ProcessEvent(AgentEvent agentEvent)
    {
        if (agentEvent.EventType == EventType.AtcComm)
        {
            await _agentManager.SendEventAsync(new AgentEvent(this)
            {
                EventType = EventType.NotifyFrontEnd,
                FrontEndMessage = $"New Flight Plan required. Checking alternates",
            });
            await Task.Delay(1000);
            await _agentManager.SendEventAsync(new AgentEvent(this)
            {
                EventType = EventType.NewDestination,
                FrontEndMessage = $"New Destination: ZURICH",
            });
        }
    }
}
