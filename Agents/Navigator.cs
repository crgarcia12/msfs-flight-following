using MSFSFlightFollowing.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MSFSFlightFollowing;

public class Navigator : AgentBase
{
    bool crossed_10k = false;
    Stopwatch watchFuel { get; set; } = Stopwatch.StartNew();
    Stopwatch watchAP { get; set; } = Stopwatch.StartNew();

    public Navigator(AgentManager agentManager) : base(agentManager, nameof(Navigator))
    {
    }

    public override async Task ProcessEvent(AgentEvent agentEvent)
    {
        if(agentEvent.EventType == EventType.NewDestination)
        {
            await _agentManager.SendEventAsync(new AgentEvent(this)
            {
                EventType = EventType.LandingRunaway,
                FrontEndMessage = $"New Destination: ZURICH - Recommend Runaway 28",
            });
        }
        if (agentEvent.EventType == EventType.AircraftDataUpdated)
        {
            var clientData = (ClientData)agentEvent.Data;

            if (!crossed_10k && clientData.Data.Altitude > 10000)
            {
                crossed_10k = true;
                await _agentManager.SendEventAsync(new AgentEvent(this)
                {
                    EventType = EventType.NotifyFrontEnd,
                    FrontEndMessage = $"Crossed 10.000 feet",
                    CopilotCommand = "Altitude check"
                });
            }

            //if (watchFuel.ElapsedMilliseconds > 30000)
            //{
            //    watchFuel = Stopwatch.StartNew();
            //    await _agentManager.SendEventAsync(new AgentEvent(this)
            //    {
            //        EventType = EventType.CopilotCommand,
            //        FrontEndMessage = $"Check Remaining Fuel",
            //        CopilotCommand = $"Remaining Fuel {Math.Floor(clientData.Data.CurrentFuel)}"
            //    });
                
            //}
            //if (watchAP.ElapsedMilliseconds > 45000)
            //{
            //    watchAP = Stopwatch.StartNew();
            //    await _agentManager.SendEventAsync(new AgentEvent(this)
            //    {
            //        EventType = EventType.CopilotCommand,
            //        FrontEndMessage = $"Check Autopilot",
            //        CopilotCommand = $"Autopilot Mode NAV: {clientData.Data.Autopilot.Nav1}"
            //    });
            //}

        }
    }
}