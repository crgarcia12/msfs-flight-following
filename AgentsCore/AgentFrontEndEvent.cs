namespace MSFSFlightFollowing.AgentsCore;

/// <summary>
/// DTO sent to the browser whenever an agent publishes a checklist callout.
/// The wire shape must remain <c>{ agent, message }</c> for the Vue front-end.
/// </summary>
public sealed class AgentFrontEndEvent
{
    public string agent { get; set; } = "";
    public string message { get; set; } = "";
}
