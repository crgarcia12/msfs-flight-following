using System.Threading.Tasks;

namespace MSFSFlightFollowing.AgentsCore;

/// <summary>
/// Narrow capability exposing the FlyByWire MCDU automations used by the agents.
/// Implemented by <see cref="MSFSFlightFollowing.SimBridgeClient"/>.
/// </summary>
public interface ISimBridgeMcdu
{
    bool IsConnected { get; }
    Task ChangeAirportAsync();
}
