using MSFSFlightFollowing.Models;
using System.Threading.Tasks;

namespace MSFSFlightFollowing;

public class Copilot : AgentBase
{
    bool crossed_10k = false;
    bool descent_bellow_10k = false;

    public Copilot(AgentManager agentManager) : base(agentManager, nameof(Copilot))
    {

    }

    public override async Task ProcessEvent(AgentEvent agentEvent)
    {
        if (agentEvent.EventType == EventType.AircraftDataUpdated)
        {
            var clientData = (ClientData)agentEvent.Data;

            double altitude = clientData.Data.Altitude;
            if (!crossed_10k && altitude > 10000)
            {
                crossed_10k = true;
                await _agentManager.SendEventAsync(new AgentEvent(this)
                {
                    EventType = EventType.CopilotCommand,
                    FrontEndMessage = $"Turn lights off",
                    CopilotCommand = "Landing lights off"
                });
                await _agentManager.SendEventAsync(new AgentEvent(this)
                {
                    EventType = EventType.CopilotCommand,
                    FrontEndMessage = $"Turn seatbelt sign off",
                    CopilotCommand = "Seatbelt sign off"
                });
            }
            if (!descent_bellow_10k && altitude < 10000)
            {
                descent_bellow_10k = true;
                await _agentManager.SendEventAsync(new AgentEvent(this)
                {
                    EventType = EventType.CopilotCommand,
                    FrontEndMessage = $"Turn landing lights on",
                    CopilotCommand = "Landing Lights on"

                });
                await _agentManager.SendEventAsync(new AgentEvent(this)
                {
                    EventType = EventType.CopilotCommand,
                    FrontEndMessage = $"Turn seatbelt sign on",
                    CopilotCommand = "Seatbelt sign on"
                });
            }
        }
    }
}
