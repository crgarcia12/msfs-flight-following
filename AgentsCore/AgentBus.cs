using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MSFSFlightFollowing.AgentsCore;

public sealed class AgentBus : IAgentBus
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<Func<object, Task>>> _handlers = new();
    private readonly ILogger<AgentBus> _logger;

    public AgentBus(ILogger<AgentBus> logger)
    {
        _logger = logger;
    }

    public IDisposable Subscribe<TMessage>(Func<TMessage, Task> handler)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        Func<object, Task> wrapper = obj => handler((TMessage)obj);
        lock (_gate)
        {
            if (!_handlers.TryGetValue(typeof(TMessage), out var list))
            {
                list = new List<Func<object, Task>>();
                _handlers[typeof(TMessage)] = list;
            }
            list.Add(wrapper);
        }
        return new Subscription(this, typeof(TMessage), wrapper);
    }

    public async Task PublishAsync<TMessage>(TMessage message)
    {
        Func<object, Task>[] snapshot;
        lock (_gate)
        {
            if (!_handlers.TryGetValue(typeof(TMessage), out var list) || list.Count == 0)
                return;
            snapshot = list.ToArray();
        }

        foreach (var handler in snapshot)
        {
            try
            {
                await handler(message!).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Agent handler for {Message} threw", typeof(TMessage).Name);
            }
        }
    }

    private void Unsubscribe(Type messageType, Func<object, Task> wrapper)
    {
        lock (_gate)
        {
            if (_handlers.TryGetValue(messageType, out var list))
                list.Remove(wrapper);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private AgentBus? _bus;
        private readonly Type _type;
        private readonly Func<object, Task> _wrapper;

        public Subscription(AgentBus bus, Type type, Func<object, Task> wrapper)
        {
            _bus = bus;
            _type = type;
            _wrapper = wrapper;
        }

        public void Dispose()
        {
            var bus = System.Threading.Interlocked.Exchange(ref _bus, null);
            bus?.Unsubscribe(_type, _wrapper);
        }
    }
}
