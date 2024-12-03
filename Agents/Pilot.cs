using MSFSFlightFollowing.Models;
using System.Threading.Tasks;

namespace MSFSFlightFollowing;

public class Pilot : AgentBase
{
    bool crossed3kasc = false;
    bool crossed3desc = false;


    public Pilot(AgentManager agentManager) : base(agentManager, nameof(Pilot))
    {
    }

    public override async Task ProcessEvent(AgentEvent agentEvent)
    {
        if (agentEvent.EventType == EventType.CopilotCommand)
        {
            await Task.Delay(1000);
            await _agentManager.SendEventAsync(new AgentEvent(this)
            {
                EventType = EventType.NotifyFrontEnd,
                FrontEndMessage = agentEvent.CopilotCommand
            });
        }
    }
}
