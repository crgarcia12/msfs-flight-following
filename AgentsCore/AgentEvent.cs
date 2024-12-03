namespace MSFSFlightFollowing;

public enum EventType
{
    NotifyFrontEnd,
    AircraftDataUpdated,
    CopilotCommand,
    AtcComm,
    NewDestination,
    LandingRunaway
}

public class AgentEvent
{
    public EventType EventType { get; set; }
    public object Sender { get; set; }
    public object Data { get; set; }
    public string FrontEndMessage { get; set; }

    public string CopilotCommand {get;set;}


    public AgentEvent(object sender)
    {
        Sender = sender;
    }
}
