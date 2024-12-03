using MSFSFlightFollowing.Models;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MSFSFlightFollowing;

public class Comms : AgentBase
{
    Stopwatch watchDeviation { get; set; } = Stopwatch.StartNew();
    bool initiated = false;

    public Comms(AgentManager agentManager) : base(agentManager, nameof(Comms))
    {
        _agentManager.SimBridgeClient.Connect();
    }

    public override async Task ProcessEvent(AgentEvent agentEvent)
    {
        if (watchDeviation.ElapsedMilliseconds < 1000 || initiated)
        {
            return;
        }

        if (agentEvent.EventType == EventType.AircraftDataUpdated)
        {
            initiated = true;
            var clientData = (ClientData)agentEvent.Data;
            await _agentManager.SimBridgeClient.ChangeAirport();
        }
    }
}
