using MSFSFlightFollowing.Models;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MSFSFlightFollowing;

public class Comms : AgentBase
{
    Stopwatch _watchDeviation;
    bool initiated = false;

    public Comms(AgentManager agentManager) : base(agentManager, nameof(Comms))
    {
        _agentManager.SimBridgeClient.Connect();
    }

    public override async Task ProcessEvent(AgentEvent agentEvent)
    {
        if (initiated)
        {
            return;
        }
        if (agentEvent.EventType != EventType.AircraftDataUpdated)
        {
            return;
        }

        var clientData = (ClientData)agentEvent.Data;
        double altitude = clientData.Data.Altitude;
        if (altitude < 7000)
        {
            return;
        }
        if (_watchDeviation == null)
        {
            _watchDeviation = Stopwatch.StartNew();
            return;
        }
        if (_watchDeviation.ElapsedMilliseconds < 5000)
        {
            return;
        }

        initiated = true;
        await _agentManager.SendEventAsync(new AgentEvent(this)
        {
            EventType = EventType.AtcComm,
            FrontEndMessage = $"ATC: Stuttgart airport is closed due to bad weather!",
            CopilotCommand = "Checked"
        });

        await _agentManager.SendEventAsync(new AgentEvent(this)
        {
            EventType = EventType.CopilotCommand,
            FrontEndMessage = $"Validate new route",
            CopilotCommand = "Landing at Zurich"
        });
    }
}
