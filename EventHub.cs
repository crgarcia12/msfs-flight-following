using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSFSFlightFollowing.Models;

namespace FSUIPCWinformsAutoCS
{
    public class EventHub
    {
        private readonly FeatureOptions _features;
        private readonly ILogger<EventHub> _logger;
        private readonly Lazy<EventHubProducerClient> _producer;
        private bool _disabledDueToError;

        public bool Enabled => _features.AzureEventHub.Enabled && !_disabledDueToError;
        public bool LastSendOk { get; private set; }

        public EventHub(IOptions<FeatureOptions> features, ILogger<EventHub> logger)
        {
            _features = features.Value;
            _logger = logger;
            _producer = new Lazy<EventHubProducerClient>(() => new EventHubProducerClient(
                _features.AzureEventHub.Namespace,
                _features.AzureEventHub.Hub,
                new AzureCliCredential()));
        }

        public async Task SendEventAsync(ClientData eventBody)
        {
            if (!Enabled) return;

            try
            {
                using EventDataBatch batch = await _producer.Value.CreateBatchAsync();
                string jsonBody = JsonSerializer.Serialize(eventBody);
                batch.TryAdd(new EventData(jsonBody) { ContentType = "application/json" });
                await _producer.Value.SendAsync(batch);
                LastSendOk = true;
            }
            catch (Exception ex)
            {
                LastSendOk = false;
                _disabledDueToError = true;
                _logger.LogWarning("Azure Event Hub disabled after error: {Message}", ex.Message);
            }
        }
    }
}
