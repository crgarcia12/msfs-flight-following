namespace MSFSFlightFollowing.Models;

public class FeatureOptions
{
    public const string SectionName = "Features";

    public AzureEventHubOptions AzureEventHub { get; set; } = new();
    public SimBridgeOptions SimBridge { get; set; } = new();
    public AgentsOptions Agents { get; set; } = new();
    public VatsimOptions Vatsim { get; set; } = new();
    public DescentOptions Descent { get; set; } = new();
    public SimOptions Sim { get; set; } = new();

    public class AzureEventHubOptions
    {
        public bool Enabled { get; set; } = false;
        public string Namespace { get; set; } = "";
        public string Hub { get; set; } = "";
    }

    public class SimOptions
    {
        /// <summary>
        /// Master switch that lets the app TRANSMIT events to MSFS (FCU knobs,
        /// autopilot mode pushes, etc.). When <c>false</c> the app is strictly
        /// read-only — it still reads SimConnect data and renders the HUD/MCDU,
        /// but every <see cref="MSFSFlightFollowing.SimConnect.ISimCommands"/>
        /// write is silently dropped. Defaults to <c>false</c> as a safety net
        /// so that opening the app mid-flight cannot move the aircraft.
        /// </summary>
        public bool WriteEnabled { get; set; } = false;
    }

    public class SimBridgeOptions
    {
        public bool Enabled { get; set; } = false;
        public string Url { get; set; } = "ws://localhost:8380/interfaces/v1/mcdu";
    }

    public class AgentsOptions
    {
        public bool Enabled { get; set; } = true;
    }

    public class VatsimOptions
    {
        public bool Enabled { get; set; } = false;

        /// <summary>How often the data feed is polled, in seconds. Minimum 15.</summary>
        public int PollIntervalSeconds { get; set; } = 20;

        /// <summary>Override data URL — useful for tests or a private mirror.</summary>
        public string DataUrl { get; set; } = "https://data.vatsim.net/v3/vatsim-data.json";

        /// <summary>Override transceivers URL.</summary>
        public string TransceiversUrl { get; set; } = "https://data.vatsim.net/v3/transceivers-data.json";

        /// <summary>How wide (NM) to extend a controller's visual range when deciding if it covers us. 0 = trust VATSIM exactly.</summary>
        public int RangeBufferNm { get; set; } = 0;
    }

    public class DescentOptions
    {
        /// <summary>Auto-fire when the FBW A32NX / Headwind A330 raises "T/D REACHED".</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>FCU altitude (feet) the agent dials in at T/D before pushing ALT for managed descent.</summary>
        public int TargetAltitudeFeet { get; set; } = 10000;

        /// <summary>Minimum spacing between auto-fires, to debounce noisy LVars.</summary>
        public int CooldownSeconds { get; set; } = 60;
    }
}
