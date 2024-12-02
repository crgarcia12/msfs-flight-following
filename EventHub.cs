using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using System.Text;
using MSFSFlightFollowing.Models;
using System.Text.Json;

namespace FSUIPCWinformsAutoCS
{
    internal class EventHub
    {
        EventHubProducerClient _producerClient;

        public EventHub() {
            _producerClient = new EventHubProducerClient(
                "crgar-eventhub.servicebus.windows.net",
                "msfs",
                new AzureCliCredential());
        }


        public async Task SendEventAsync(ClientData eventBody)
        {


            using EventDataBatch eventBatch = await _producerClient.CreateBatchAsync();

            // Serialize the event body to JSON
            string jsonBody = JsonSerializer.Serialize(eventBody);
            EventData eventData = new EventData(jsonBody)
            {
                ContentType = "application/json",
            };
            eventBatch.TryAdd(eventData);

            // Use the producer client to send the batch of events to the event hub
            await _producerClient.SendAsync(eventBatch);
        }
    }
}
