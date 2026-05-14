using System;
using MSFSFlightFollowing.Models;

namespace MSFSFlightFollowing.SimConnect;

/// <summary>
/// Event args carrying a freshly-marshaled aircraft state. Raised on the SimConnect
/// message-pump thread — downstream consumers should hand the snapshot to a queue
/// rather than do real work on this thread.
/// </summary>
public sealed class AircraftSnapshotEventArgs : EventArgs
{
    public AircraftStatusModel Aircraft { get; }
    public AircraftSnapshotEventArgs(AircraftStatusModel aircraft) => Aircraft = aircraft;
}
