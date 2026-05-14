using System;
using System.Threading.Tasks;

namespace MSFSFlightFollowing.AgentsCore;

/// <summary>
/// Type-safe in-process pub/sub used by the cockpit agents.
/// Subscribers receive only the message types they ask for.
/// </summary>
public interface IAgentBus
{
    IDisposable Subscribe<TMessage>(Func<TMessage, Task> handler);
    Task PublishAsync<TMessage>(TMessage message);
}
