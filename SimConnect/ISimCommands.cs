namespace MSFSFlightFollowing.SimConnect;

/// <summary>
/// Narrow capability that exposes only the simulator commands an agent is allowed to issue.
/// Implemented by <see cref="SimConnector"/>.
/// </summary>
public interface ISimCommands
{
    bool IsConnected { get; }

    /// <summary>
    /// Set the autopilot target altitude (feet) and engage altitude hold.
    /// </summary>
    void BeginDescent(int targetAltitudeFeet);

    /// <summary>
    /// Set the FCU altitude to <paramref name="currentAltitudeFeet"/> (rounded to the
    /// nearest 100 ft) and press the FCU ALT pushbutton so the A32NX captures and holds
    /// the current altitude. Used to recover from an unexpected SRS engagement.
    /// </summary>
    void EngageAltitudeHold(int currentAltitudeFeet);

    /// <summary>
    /// Push the approach button and engage both autopilots (A32NX-specific).
    /// </summary>
    void EngageApproach();

    // ---------- FCU panel (Airbus-style autopilot control) ----------
    // Every method below transmits both A32NX.* and A339X.* H: events so the same
    // command works for the FlyByWire A32NX and the Headwind A330-900 (A339X).
    // MSFS silently ignores events the aircraft doesn't subscribe to.

    void FcuSetSpeed(int knots);
    void FcuPushSpeed();
    void FcuPullSpeed();

    void FcuSetHeading(int degrees);
    void FcuPushHeading();
    void FcuPullHeading();

    void FcuSetAltitude(int feet);
    void FcuPushAltitude();
    void FcuPullAltitude();

    void FcuSetVerticalSpeed(int fpm);
    void FcuPushVerticalSpeed();
    void FcuPullVerticalSpeed();

    void FcuToggleAp1();
    void FcuToggleAp2();
    void FcuToggleAthr();
    void FcuPushLoc();
    void FcuPushAppr();
    void FcuPushExped();
}
