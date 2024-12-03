using Microsoft.Azure.Amqp.Framing;
using MSFSFlightFollowing.Models;
using System.Threading.Tasks;

namespace MSFSFlightFollowing;

public class Copilot : AgentBase
{
    bool crossed_10k = false;
    bool descent_bellow_10k = false;

    bool crossed_3k = false;
    bool descent_bellow_3k = false;

    bool start_takeoff = false;
    public Copilot(AgentManager agentManager) : base(agentManager, nameof(Copilot))
    {

    }

    public override async Task ProcessEvent(AgentEvent agentEvent)
    {
        if (agentEvent.EventType == EventType.LandingRunaway)
        {
            await _agentManager.SendEventAsync(new AgentEvent(this)
            {
                EventType = EventType.CopilotCommand,
                FrontEndMessage = $"Initiate descent for Runaway 28",
                CopilotCommand = "Initiating descent to 4000 feet"
            });
            base._agentManager.SimConnector.StartDecent();

            await _agentManager.SendEventAsync(new AgentEvent(this)
            {
                EventType = EventType.CopilotCommand,
                FrontEndMessage = $"Start configuring Autopilot Computer",
                CopilotCommand = "Configuring Autopilot computer"
            });
            base._agentManager.SimBridgeClient.ChangeAirport();
            await _agentManager.SendEventAsync(new AgentEvent(this)
            {
                EventType = EventType.CopilotCommand,
                FrontEndMessage = $"Copmputer configured",
                CopilotCommand = "Check computer configured"
            });
        }

        if (agentEvent.EventType == EventType.AircraftDataUpdated)
        {
            var clientData = (ClientData)agentEvent.Data;
            double altitude = clientData.Data.Altitude;
            double airspeed = clientData.Data.AirspeedIndicated;


            if (!start_takeoff && airspeed > 5)
            {
                start_takeoff = true;
                await _agentManager.SendEventAsync(new AgentEvent(this)
                {
                    EventType = EventType.CopilotCommand,
                    FrontEndMessage = $"Ready for TakeOff",
                    CopilotCommand = "Take Off approved"
                });
            }
            /////////////////// 10k
            if (!crossed_10k && altitude > 10000)
            {
                crossed_10k = true;
                await _agentManager.SendEventAsync(new AgentEvent(this)
                {
                    EventType = EventType.CopilotCommand,
                    FrontEndMessage = $"Turn lights off",
                    CopilotCommand = "Landing lights off"
                });
            }
            if (crossed_10k && !descent_bellow_10k && altitude < 10000)
            {
                descent_bellow_10k = true;
                await _agentManager.SendEventAsync(new AgentEvent(this)
                {
                    EventType = EventType.CopilotCommand,
                    FrontEndMessage = $"Turn landing lights on",
                    CopilotCommand = "Landing Lights on"

                });
            }

            /////////////////// 3k
            if (!crossed_3k && altitude > 3000)
            {
                crossed_3k = true;
                await _agentManager.SendEventAsync(new AgentEvent(this)
                {
                    EventType = EventType.CopilotCommand,
                    FrontEndMessage = $"Turn on Autopilot",
                    CopilotCommand = "Autopilot ON"
                });
            }
            if (crossed_3k && !descent_bellow_3k && altitude < 3000)
            {
                descent_bellow_3k = true;
                await _agentManager.SendEventAsync(new AgentEvent(this)
                {
                    EventType = EventType.CopilotCommand,
                    FrontEndMessage = $"Set landing AP",
                    CopilotCommand = "AP autolading on"

                });
                base._agentManager.SimConnector.StartApproach();
            }
        }
    }
}
