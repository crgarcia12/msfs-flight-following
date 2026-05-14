using Microsoft.FlightSimulator.SimConnect;
using static MSFSFlightFollowing.Models.SimConnectStructs;

namespace MSFSFlightFollowing.SimConnect;

/// <summary>
/// Central registry of every SimConnect data variable we read into <see cref="MSFSFlightFollowing.Models.SimConnectStructs.AircraftStatusStruct"/>.
/// Keeping all definitions in one place makes it obvious which sim variables are wired up.
/// </summary>
internal static class SimConnectDataDefinitions
{
    private sealed record SimVar(string Name, string Unit, SIMCONNECT_DATATYPE Type);

    private static readonly SimVar[] AircraftStatusVars =
    {
        // Aircraft position / motion
        new("PLANE LATITUDE",                   "Degrees",          SIMCONNECT_DATATYPE.FLOAT64),
        new("PLANE LONGITUDE",                  "Degrees",          SIMCONNECT_DATATYPE.FLOAT64),
        new("INDICATED ALTITUDE",               "feet",             SIMCONNECT_DATATYPE.FLOAT64),
        new("FUEL TOTAL QUANTITY",              "gallons",          SIMCONNECT_DATATYPE.FLOAT64),
        new("FUEL TOTAL CAPACITY",              "gallons",          SIMCONNECT_DATATYPE.FLOAT64),
        new("PLANE HEADING DEGREES TRUE",       "degrees",          SIMCONNECT_DATATYPE.FLOAT64),
        new("AIRSPEED INDICATED",               "knots",            SIMCONNECT_DATATYPE.FLOAT64),
        new("AIRSPEED TRUE",                    "knots",            SIMCONNECT_DATATYPE.FLOAT64),

        // Navigation
        new("NAV HAS NAV",                      "bool",             SIMCONNECT_DATATYPE.INT32),
        new("NAV HAS DME",                      "bool",             SIMCONNECT_DATATYPE.INT32),
        new("NAV DME",                          "nautical miles",   SIMCONNECT_DATATYPE.FLOAT64),
        new("GPS IS ACTIVE FLIGHT PLAN",        "bool",             SIMCONNECT_DATATYPE.INT32),
        new("GPS IS ACTIVE WAY POINT",          "bool",             SIMCONNECT_DATATYPE.INT32),
        new("GPS FLIGHT PLAN WP INDEX",         "bool",             SIMCONNECT_DATATYPE.INT32),
        new("GPS WP DISTANCE",                  "meters",           SIMCONNECT_DATATYPE.FLOAT64),
        new("GPS WP NEXT LAT",                  "degrees",          SIMCONNECT_DATATYPE.FLOAT64),
        new("GPS WP NEXT LON",                  "degrees",          SIMCONNECT_DATATYPE.FLOAT64),
        new("GPS WP PREV LAT",                  "degrees",          SIMCONNECT_DATATYPE.FLOAT64),
        new("GPS WP PREV LON",                  "degrees",          SIMCONNECT_DATATYPE.FLOAT64),
        new("GPS WP ETE",                       "seconds",          SIMCONNECT_DATATYPE.FLOAT64),

        // Autopilot
        new("AUTOPILOT AVAILABLE",              "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT MASTER",                 "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT WING LEVELER",           "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT ALTITUDE LOCK",          "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT APPROACH HOLD",          "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT BACKCOURSE HOLD",        "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT FLIGHT DIRECTOR ACTIVE", "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT AIRSPEED HOLD",          "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT MACH HOLD",              "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT YAW DAMPER",             "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOTHROTTLE ACTIVE",              "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT VERTICAL HOLD",          "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT HEADING LOCK",           "bool",             SIMCONNECT_DATATYPE.INT32),
        new("AUTOPILOT NAV1 LOCK",              "bool",             SIMCONNECT_DATATYPE.INT32),

        // A32NX (FlyByWire) display
        new("L:A32NX_FCU_AFS_DISPLAY_HDG_TRK_VALUE", "number",      SIMCONNECT_DATATYPE.INT32),

        // Attitude / dynamics (any aircraft)
        new("PLANE PITCH DEGREES",              "degrees",          SIMCONNECT_DATATYPE.FLOAT64),
        new("PLANE BANK DEGREES",               "degrees",          SIMCONNECT_DATATYPE.FLOAT64),
        new("GROUND VELOCITY",                  "knots",            SIMCONNECT_DATATYPE.FLOAT64),
        new("G FORCE",                          "GForce",           SIMCONNECT_DATATYPE.FLOAT64),

        // Atmosphere / environment
        new("AMBIENT WIND VELOCITY",            "knots",            SIMCONNECT_DATATYPE.FLOAT64),
        new("AMBIENT WIND DIRECTION",           "degrees",          SIMCONNECT_DATATYPE.FLOAT64),
        new("AMBIENT TEMPERATURE",              "celsius",          SIMCONNECT_DATATYPE.FLOAT64),

        // Radio altimeter and ground state
        new("RADIO HEIGHT",                     "feet",             SIMCONNECT_DATATYPE.FLOAT64),
        new("SIM ON GROUND",                    "bool",             SIMCONNECT_DATATYPE.INT32),

        // Flaps (works for every aircraft, also reflected in A32NX)
        new("FLAPS HANDLE INDEX",               "number",           SIMCONNECT_DATATYPE.INT32),

        // FlyByWire A32NX — extra automation insight (0 when the FBW module is not loaded)
        new("L:A32NX_AUTOTHRUST_THRUST_LIMIT_TYPE", "number",       SIMCONNECT_DATATYPE.INT32),
        new("L:A32NX_FWC_FLIGHT_PHASE",         "number",           SIMCONNECT_DATATYPE.INT32),
        new("L:A32NX_FMGC_FLIGHT_PHASE",        "number",           SIMCONNECT_DATATYPE.INT32),
        new("L:A32NX_AUTOBRAKES_ARMED_MODE",    "number",           SIMCONNECT_DATATYPE.INT32),
        new("L:A32NX_SPOILERS_ARMED",           "bool",             SIMCONNECT_DATATYPE.INT32),

        // A32NX FMA (autoflight mode annunciator) — match values in FBW's autopilot.ts.
        // Used to render the top-of-PFD FMA banner and detect unexpected SRS engagement.
        new("L:A32NX_FMA_VERTICAL_MODE",        "number",           SIMCONNECT_DATATYPE.INT32),
        new("L:A32NX_FMA_LATERAL_MODE",         "number",           SIMCONNECT_DATATYPE.INT32),
        new("L:A32NX_FMA_VERTICAL_ARMED",       "number",           SIMCONNECT_DATATYPE.INT32),
        new("L:A32NX_FMA_LATERAL_ARMED",        "number",           SIMCONNECT_DATATYPE.INT32),

        // "T/D REACHED" PFD message — raised by FBW VNAV when crossing top of descent.
        // Powers the auto-descent feature in DescentGuard.
        new("L:A32NX_PFD_MSG_TD_REACHED",       "bool",             SIMCONNECT_DATATYPE.INT32),

        // A/THR (autothrust) mode + status — leftmost column of the Airbus FMA.
        // Mode values mirror AutoThrustMode in fbw-a32nx/src/systems/shared/src/autopilot.ts.
        new("L:A32NX_AUTOTHRUST_MODE",          "number",           SIMCONNECT_DATATYPE.INT32),
        new("L:A32NX_AUTOTHRUST_STATUS",        "number",           SIMCONNECT_DATATYPE.INT32),

        // ARINC429 packed words. The FBW / Headwind `updateFmgcShim()` only maps a
        // FRACTION of the real autothrust + vertical FMA modes into the legacy
        // A32NX_AUTOTHRUST_MODE / A32NX_FMA_VERTICAL_MODE integer LVars; everything
        // else (MACH, SPEED, THR CLB, ALT CRZ, …) is encoded as bits in these two
        // discrete words. We decode them in A32nxFmaModes to recover the missing
        // FMA labels. Read as Number / FLOAT64 since they are 32-bit packed words
        // stored as a double by the WASM.
        new("L:A32NX_FCU_ATS_FMA_DISCRETE_WORD","number",           SIMCONNECT_DATATYPE.FLOAT64),
        new("L:A32NX_FMGC_1_DISCRETE_WORD_1",   "number",           SIMCONNECT_DATATYPE.FLOAT64),

        // Per-channel AP engagement (FBW reports AP1 / AP2 separately).
        new("L:A32NX_AUTOPILOT_1_ACTIVE",       "bool",             SIMCONNECT_DATATYPE.INT32),
        new("L:A32NX_AUTOPILOT_2_ACTIVE",       "bool",             SIMCONNECT_DATATYPE.INT32),
    };

    public static void Register(Microsoft.FlightSimulator.SimConnect.SimConnect simconnect)
    {
        foreach (var v in AircraftStatusVars)
        {
            simconnect.AddToDataDefinition(
                DEFINITIONS.AircraftStatus, v.Name, v.Unit, v.Type, 0.0f, Microsoft.FlightSimulator.SimConnect.SimConnect.SIMCONNECT_UNUSED);
        }
        simconnect.RegisterDataDefineStruct<AircraftStatusStruct>(DEFINITIONS.AircraftStatus);
    }
}
