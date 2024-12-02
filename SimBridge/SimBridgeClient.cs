using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MSFSFlightFollowing;

public class SimBridgeClient
{
    object fmcRootLock;
    FmcRoot _root = null;
    FmcRoot root
    {
        get { 
            lock (fmcRootLock)
            {
                return _root;
            }
        }
        set {
            lock (fmcRootLock)
            {
                _root = value;
            }
        }
    }


    private string RemoveTags(string receivedMessage)
    {
        receivedMessage = receivedMessage.Replace("{end}", "");

        receivedMessage = receivedMessage.Replace("{white}", "");
        receivedMessage = receivedMessage.Replace("{cyan}", "");
        receivedMessage = receivedMessage.Replace("{green}", "");
        receivedMessage = receivedMessage.Replace("{amber}", "");
        receivedMessage = receivedMessage.Replace("{magenta}", "");

        receivedMessage = receivedMessage.Replace("{small}", "");
        receivedMessage = receivedMessage.Replace("{big}", "");

        receivedMessage = receivedMessage.Replace("{sp}", "");

        receivedMessage = receivedMessage.Replace("{right}", "");
        receivedMessage = receivedMessage.Replace("{left}", "");

        return receivedMessage;
    }

    private async Task SendRequestUpdate(ClientWebSocket ws)
    {
        ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes("requestUpdate"));
        await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);
        await Task.Delay(500);
    }

    // Every time we press something opn the FMC, the server sends and update (so it is not needed to request an update)
    private async Task ReceiveMessagesAndUpdateRoot(ClientWebSocket ws, bool sendRequestUpdate = true)
    {
        FmcRoot root = null ;

        int retry = 4;
        while (true)
        {
            try
            {
                retry--;
                // Request update
                if (sendRequestUpdate)
                {
                    //await SendRequestUpdate(ws);
                    await Task.Delay(500);
                }

                // Receive a message from the server
                ArraySegment<byte> bytesReceived = new ArraySegment<byte>(new byte[40960]);
                WebSocketReceiveResult result = await ws.ReceiveAsync(bytesReceived, CancellationToken.None);

                // The message says update:<json>
                string receivedMessage = Encoding.UTF8.GetString(bytesReceived.Array, 0, result.Count);

                // if another window is requesting updates, we skip it
                if (receivedMessage == "requestUpdate" || receivedMessage.StartsWith("event:left"))
                {
                    sendRequestUpdate = false;
                    continue;
                }

                receivedMessage = RemoveTags(receivedMessage);
                Console.WriteLine("----------- Message Received ------------");
                Console.WriteLine(receivedMessage);
                Console.WriteLine("-----------------------------------------");
                receivedMessage = receivedMessage.Substring(7);

                // The message has tags, like {white} indicating the text color. we remove them
                root = JsonSerializer.Deserialize<FmcRoot>(receivedMessage, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                await Task.Delay(500);

            }
            catch (Exception ex)
            {
                Console.WriteLine("----------- Message Received ERROR ------------");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("(----------------------------------------------");
                if (retry <= 0)
                {
                    throw;
                }
            }
        }
    }

    private async Task Press(ClientWebSocket ws, string button)
    {
        string message = $"event:left:{button}";
        ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
        await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);
        await Task.Delay(500);

    }

    private async Task Type(ClientWebSocket ws, string text)
    {
        foreach (char character in text)
        {
            string button = character.ToString();
            if (button == "/")
            {
                button = "DIV";
            }

            await Press(ws, button);
        }
    }

    public async Task Connect()
    {
        string message;
        FmcRoot fmcRoot;

        using (ClientWebSocket ws = new ClientWebSocket())
        {
            Uri serverUri = new Uri("ws://localhost:8380/interfaces/v1/mcdu");
            await ws.ConnectAsync(serverUri, CancellationToken.None);
            await SendRequestUpdate(ws);
            await Task.Run(async () => { await ReceiveMessagesAndUpdateRoot(ws); });
            Console.WriteLine("Connected!");

            while (true)
            {
                try
                {
                    fmcRoot = await ReceiveMessage(ws);

                    while (!string.IsNullOrEmpty(fmcRoot.Left.Scratchpad))
                    {
                        await Press(ws, "CLR");
                        await Task.Delay(1000);
                        fmcRoot = await ReceiveMessage(ws);
                    }

                    // Changin INIT page to new destination
                    await Press(ws, "INIT");
                    await Type(ws, "LSGG/LSZH");
                    await Press(ws, "R1");
                    await Press(ws, "L6");
                    
                    // Configure Flight plan
                    await Press(ws, "FPLN");
                    await Press(ws, "L6"); //LSZH
                    await Press(ws, "R1"); //Arrival
                    await Press(ws, "L5"); //ILS28
                    await Press(ws, "L5"); //ILS28
                    await Press(ws, "R6"); //INSERT

                    // Direct
                    await Press(ws, "DIR");

                    fmcRoot = await ReceiveMessage(ws);

                    bool found = false;
                    while (!found)
                    {
                        int index = 0;
                        foreach (List<string> line in fmcRoot.Left.Lines)
                        {
                            // There are 3 columns in every row. I am interested in the first one (0)
                            if (line[0].Contains("LSZH28"))
                            {
                                found = true;
                                await Press(ws, $"L{index.ToString()}");
                            }
                            index++;
                        }
                        if(!found)
                        {
                            Press(ws, "UP");
                            await Task.Delay(1000);
                            fmcRoot = await ReceiveMessage(ws);
                        }
                    }

                    // "scratchpad": "{white}T/D REACHED{end}",

                    // Close the WebSocket connection
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    Console.WriteLine("Connection closed.");

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}
