using MSFSFlightFollowing.Models;

namespace MSFSFlightFollowing.AgentsCore;

/// <summary>
/// Strongly-typed messages exchanged on the agent bus. Records keep them immutable
/// and make handler signatures self-documenting.
/// </summary>

/// <summary>A 1 Hz sample of aircraft state from SimConnect.</summary>
public sealed record AircraftSnapshot(AircraftStatusModel Aircraft);

/// <summary>Emitted by FlightPhaseDetector when the derived flight phase changes.</summary>
public sealed record FlightPhaseChanged(string From, string To);

/// <summary>Emitted by AltitudeCalloutEmitter when the aircraft crosses a notable altitude.</summary>
public sealed record AltitudeCallout(int Feet, bool Ascending);

/// <summary>Emitted by TakeoffDetector the first time the aircraft starts moving for takeoff.</summary>
public sealed record TakeoffStarted();

/// <summary>An ATC transmission delivered to the cockpit.</summary>
public sealed record AtcMessage(string Text);

/// <summary>A new destination has been assigned (e.g. by Operations after a diversion).</summary>
public sealed record DestinationAssigned(string Icao);

/// <summary>An approach has been cleared by the Navigator for the assigned destination.</summary>
public sealed record ApproachCleared(string Icao, string Runway);

/// <summary>
/// A spoken callout shown on the UI (right-rail timeline).
/// If <see cref="PilotResponse"/> is non-empty, the Pilot agent echoes it after a short delay.
/// </summary>
public sealed record ChecklistCallout(string Agent, string Message, string PilotResponse = "");

/// <summary>Sent to the front-end when the sim disconnects so detector state can reset.</summary>
public sealed record SimDisconnected();

/// <summary>The set of VATSIM controllers covering the aircraft has changed.</summary>
public sealed record NearbyControllersChanged(
    System.Collections.Generic.IReadOnlyList<Vatsim.NearbyController> Controllers,
    System.Collections.Generic.IReadOnlyList<Vatsim.NearbyController> Entered,
    System.Collections.Generic.IReadOnlyList<string> Left);

/// <summary>Periodic refresh of the same VATSIM list (set unchanged, distances updated).</summary>
public sealed record VatsimRefreshed(System.Collections.Generic.IReadOnlyList<Vatsim.NearbyController> Controllers);

/// <summary>A VATSIM ATIS station has published a new information letter.</summary>
public sealed record AtisUpdated(string Callsign, string AtisCode, string Text);

/// <summary>The A32NX is in SRS pitch mode outside takeoff/go-around — likely an accidental TOGA.</summary>
public sealed record UnexpectedSrsDetected(int CurrentAltitudeFt, int FmgcFlightPhase);
