using System;
using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;
using static MSFSFlightFollowing.Models.SimConnectStructs;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using FSUIPCWinformsAutoCS;
using System.Text.RegularExpressions;
using static MSFSFlightFollowing.Models.SimConnector;
using System.Diagnostics.Eventing.Reader;

namespace MSFSFlightFollowing.Models
{
    public class AgentFrontEndEvent
    {
        public string agent { get; set; }
        public string message { get; set; }
    }


    public class ClientData
    {
        public bool IsConnected { get; set; }
        public AircraftStatusModel Data { get; set; }
    }

    public class SimConnector
    {
        private static int eventssent = 0;

        public AircraftStatusModel AircraftStatus { get; private set; }
        public bool IsConnected => simconnect != null;

        private readonly ILogger<SimConnector> _logger;
        private IntPtr WindowHandle { get; }
        private CancellationTokenSource cancellationToken;
        private SimConnect simconnect = null;
        private IHubContext<WebSocketConnector> _wsConnector;
        private IHostApplicationLifetime _lifetime;
        private IWebHostEnvironment _env;
        private EventHub _eventHub;
        private AgentManager _agentManager;

        const uint WM_USER_SIMCONNECT = 0x0402;

        public SimConnector(IHubContext<WebSocketConnector> wsConnector, AgentManager agentManager, ILogger<SimConnector> logger, IHostApplicationLifetime lifetime, IWebHostEnvironment env)
        {
            _eventHub = new EventHub();
            _logger = logger;
            _wsConnector = wsConnector;
            _lifetime = lifetime;
            _env = env;
            _agentManager = agentManager;

            _lifetime.ApplicationStopping.Register(Disconnect);

            MessageWindow win = MessageWindow.GetWindow();
            WindowHandle = win.Hwnd;
            win.WndProcHandle += W_WndProcHandle;

            cancellationToken = new CancellationTokenSource();

            // Enable for sending test data to client
            //TestDataRunner();
        }

        public void Connect()
        {
            if (simconnect != null)
                return;

            try
            {
                simconnect = new SimConnect("MSFS Flight Following", WindowHandle, WM_USER_SIMCONNECT, null, 0);

                simconnect.OnRecvOpen += OnRecvOpen;
                simconnect.OnRecvQuit += OnRecvQuit;
                simconnect.OnRecvException += RecvExceptionHandler;
                simconnect.OnRecvSimobjectDataBytype += RecvSimobjectDataBytype;
            }
            catch (COMException ex)
            {
                _logger.LogError("Unable to create new SimConnect instance: {0}", ex.Message);
                simconnect = null;
            }
        }

        private IntPtr W_WndProcHandle(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (msg == WM_USER_SIMCONNECT)
                    ReceiveSimConnectMessage();
            }
            catch
            {
                Disconnect();
            }

            return IntPtr.Zero;
        }

        private void ReceiveSimConnectMessage()
        {
            simconnect?.ReceiveMessage();
        }

        private void OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            SetFlightDataDefinitions();
            Task.Run(async () =>
            {
                //Carlos
                MapEvents();

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                    if (WebSocketConnector.userCount > 0)
                    {
                        simconnect?.RequestDataOnSimObjectType(DATA_REQUEST.AircraftStatus, DEFINITIONS.AircraftStatus, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);

                        //carlos
                        SetHeading(123);
                    }
                }
            });

            
            _logger.LogInformation("Simconnect has connected to the flight sim.");
        }

        private void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Disconnect();
        }

        private void RecvExceptionHandler(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            _logger.LogError("SimConnect exception: {0}", data.dwException);
            Disconnect();
        }

        private async Task SendAgentEventsToFrontEnd()
        {
            
            //if (eventssent++%3 != 0) return;

            //await Task.Delay(5000);
            //var agentEvent = new AgentFrontEndEvent()
            //{
            //    agent = "comms",
            //    message = "Innsbruck airport is closed due to weather"
            //};
            //await _wsConnector.Clients.All.SendAsync("ReceiveAgentEvent", agentEvent);
            //agentEvent.agent = "operator";
            //agentEvent.message = "Searching for closest alternative in database";
            //await Task.Delay(2000);
            //await _wsConnector.Clients.All.SendAsync("ReceiveAgentEvent", agentEvent);
            //agentEvent.agent = "operator";
            //agentEvent.message = "Propose deviation to Zurich";
            //await Task.Delay(1000);
            //await _wsConnector.Clients.All.SendAsync("ReceiveAgentEvent", agentEvent);
            //agentEvent.agent = "navigator";
            //agentEvent.message = "Route to Zurich: ASOBO UW15 ZL75 ZRH";
            //await Task.Delay(1000);
            //await _wsConnector.Clients.All.SendAsync("ReceiveAgentEvent", agentEvent);
            //agentEvent.agent = "navigator";
            //agentEvent.message = "Active runaway: 28";
            //await Task.Delay(500);
            //await _wsConnector.Clients.All.SendAsync("ReceiveAgentEvent", agentEvent);
            //agentEvent.agent = "pilot";
            //agentEvent.message = "Deviating to ZRH RWY 28";
            //await _wsConnector.Clients.All.SendAsync("ReceiveAgentEvent", agentEvent);
        }


        private void RecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            
            switch (data.dwRequestID)
            {
                case (uint)DATA_REQUEST.AircraftStatus:
                    AircraftStatus = new AircraftStatusModel((AircraftStatusStruct)data.dwData[0]);
                    ClientData clientData = new ClientData()
                    {
                        IsConnected = true,
                        Data = AircraftStatus
                    };
                    _wsConnector.Clients.All.SendAsync("ReceiveData", clientData);


                    Task.Run(() => SendAgentEventsToFrontEnd());

                    Task.Run(async () => {
                        await this._agentManager.SendEventAsync(new AgentEvent(this)
                        {
                            Data = clientData,
                            EventType = EventType.AircraftDataUpdated
                        });
                    });

                    Task.Run(() => _eventHub.SendEventAsync(clientData));
                    
                    break;
            }
        }

        private void Disconnect()
        {
            ClientData clientData = new ClientData()
            {
                IsConnected = false
            };
            _wsConnector.Clients.All.SendAsync("ReceiveData", clientData);

            if (simconnect == null)
                return;

            cancellationToken.Cancel();

            simconnect.Dispose();
            simconnect = null;

            _logger.LogInformation("SimConnect was disconnected from the flight sim.");
        }



        private void SetFlightDataDefinitions()
        {
            #region Aircraft Properties
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "PLANE LATITUDE", "Degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "PLANE LONGITUDE", "Degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "FUEL TOTAL QUANTITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "FUEL TOTAL CAPACITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AIRSPEED INDICATED", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AIRSPEED TRUE", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            #endregion

            #region Nav Properties
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "NAV HAS NAV", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "NAV HAS DME", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "NAV DME", "nautical miles", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "GPS IS ACTIVE FLIGHT PLAN", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "GPS IS ACTIVE WAY POINT", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "GPS FLIGHT PLAN WP INDEX", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "GPS WP DISTANCE", "meters", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "GPS WP NEXT LAT", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "GPS WP NEXT LON", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "GPS WP PREV LAT", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "GPS WP PREV LON", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "GPS WP ETE", "seconds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            #endregion

            #region Autopilot Properties
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT AVAILABLE", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT MASTER", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT WING LEVELER", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT ALTITUDE LOCK", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT APPROACH HOLD", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT BACKCOURSE HOLD", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT FLIGHT DIRECTOR ACTIVE", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT AIRSPEED HOLD", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT MACH HOLD", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT YAW DAMPER", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOTHROTTLE ACTIVE", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT VERTICAL HOLD", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT HEADING LOCK", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "AUTOPILOT NAV1 LOCK", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            #endregion

            #region FBW definitions
            simconnect.AddToDataDefinition(DEFINITIONS.AircraftStatus, "L:A32NX_FCU_AFS_DISPLAY_HDG_TRK_VALUE", "number", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            #endregion

            simconnect.RegisterDataDefineStruct<AircraftStatusStruct>(DEFINITIONS.AircraftStatus);
        }



        #region Carlos

        enum EVENTS
        {
            UNPAUSE = 0,
            PAUSE,
            GEARUP,
            GEARDOWN,
            HEADING
        };

        public enum hSimconnect : int
        {
            group1
        }
        private void MapEvents()
        {
            simconnect.MapClientEventToSimEvent(EVENTS.GEARDOWN, "TOGGLE_BEACON_LIGHTS");
            simconnect.MapClientEventToSimEvent(EVENTS.HEADING, "A32NX.FCU_HDG_SET");
        }

        private void SetHeading(int heading)
        {
            //simconnect.TransmitClientEvent((uint)SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.GEARDOWN, (uint)0, hSimconnect.group1, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
            //simconnect.TransmitClientEvent((uint)SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.HEADING, (uint)170, hSimconnect.group1, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
        }
        #endregion

        #region TestData
        public void TestDataRunner()
        {
            if (!_env.IsDevelopment())
                return;

            Thread runner = new Thread((obj) =>
            {
                while (true)
                {
                    Thread.Sleep(1000);
                    _wsConnector.Clients.All.SendAsync("ReceiveData", GenTestData());
                }
            });
            runner.IsBackground = true;
            runner.Start();
        }

        private ClientData GenTestData()
        {
            var wsData = new ClientData()
            {
                IsConnected = true,
                Data = AircraftStatusModel.GetDummyData()
            };
            return wsData;
        }
        #endregion
    }
}

