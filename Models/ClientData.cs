namespace MSFSFlightFollowing.Models;

/// <summary>
/// The payload sent to the browser on every <c>ReceiveData</c> SignalR message.
/// The shape of this class is part of the public wire contract with <c>main.js</c>
/// and must remain stable.
/// </summary>
public sealed class ClientData
{
    public bool IsConnected { get; set; }
    public AircraftStatusModel? Data { get; set; }
    public ServicesStatus Services { get; set; } = new();
    /// <summary>Latest MCDU snapshot from FlyByWire SimBridge (or <c>null</c> if not connected).</summary>
    public MSFSFlightFollowing.FmcRoot? Mcdu { get; set; }
}

/// <summary>
/// Health badges shown in the HUD. Each field maps directly to a UI indicator.
/// </summary>
public sealed class ServicesStatus
{
    public bool Sim { get; set; }
    public bool SimBridge { get; set; }
    public bool EventHub { get; set; }
    /// <summary>
    /// More detailed SimBridge state — surfaced as a string ("Disabled",
    /// "Connecting", "AwaitingAircraft", "Streaming") so the front-end can
    /// show actionable guidance when the MCDU pane is empty.
    /// </summary>
    public string SimBridgeStatus { get; set; } = "Disabled";

    /// <summary>
    /// Mirrors <c>Features.Sim.WriteEnabled</c>. When <c>false</c>, the UI
    /// renders a READ-ONLY badge and disables every FCU control so the user
    /// cannot accidentally move the aircraft.
    /// </summary>
    public bool SimWriteEnabled { get; set; }
}
