using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSFSFlightFollowing.Models
{
    /// <summary>
    /// SignalR hub for browser clients. Tracks the connected-client count via
    /// <see cref="Interlocked"/> so consumers (e.g. <see cref="MSFSFlightFollowing.SimConnect.SimConnector"/>'s
    /// polling loop) can avoid asking the sim for data when nobody is listening.
    /// </summary>
    public class WebSocketConnector : Hub
    {
        private static int s_connectedClients;

        public static bool HasConnectedClients() => Volatile.Read(ref s_connectedClients) > 0;
        public static int ConnectedClients => Volatile.Read(ref s_connectedClients);

        public override Task OnConnectedAsync()
        {
            Interlocked.Increment(ref s_connectedClients);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            Interlocked.Decrement(ref s_connectedClients);
            return base.OnDisconnectedAsync(exception);
        }

        public async Task SendData(string data)
        {
            await Clients.All.SendAsync("ReceiveData", data);
        }
    }
}