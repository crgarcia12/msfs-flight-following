using System.Runtime.InteropServices;

namespace MSFSFlightFollowing.Models
{
    public class SimConnectStructs
    {
        public enum DEFINITIONS
        {
            AircraftStatus
        }

        public enum DATA_REQUEST
        {
            AircraftStatus
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct AircraftStatusStruct
        {
            public double Latitude;
            public double Longitude;
            public double Altitude;
            public double CurrentFuel;
            public double TotalFuel;
            public double TrueHeading;
            public double AirspeedIndicated;
            public double AirspeedTrue;

            public bool NavHasSignal;
            public bool NavHasDME;
            public double DMEDistance;
            public bool GPSFlightPlanActive;
            public bool GPSWaypointModeActive;
            public int GPSWaypointIndex;
            public double GPSWaypointDistance;
            public double GPSNextWPLatitude;
            public double GPSNextWPLongitude;
            public double GPSPrevWPLatitude;
            public double GPSPrevWPLongitude;
            public double GPSWPETE;

            public bool AutopilotAvailable;
            public bool AutopilotMaster;
            public bool AutopilotWingLevel;
            public bool AutopilotAltitude;
            public bool AutopilotApproach;
            public bool AutopilotBackcourse;
            public bool AutopilotFlightDirector;
            public bool AutopilotAirspeed;
            public bool AutopilotMach;
            public bool AutopilotYawDamper;
            public bool AutopilotAutothrottle;
            public bool AutopilotVerticalHold;
            public bool AutopilotHeading;
            public bool AutopilotNav1;
            public uint DISPLAY_HDG_TRK;

            // ---- Attitude / dynamics ----
            public double PitchDegrees;
            public double BankDegrees;
            public double GroundSpeedKnots;
            public double GForce;

            // ---- Environment ----
            public double WindVelocityKnots;
            public double WindDirectionDegrees;
            public double OutsideAirTempC;

            // ---- Radio altimeter + ground ----
            public double RadioAltitudeFeet;
            public bool OnGround;

            // ---- Configuration ----
            public int FlapsHandleIndex;

            // ---- FlyByWire A32NX ----
            public int A32nxThrustLimitType;
            public int A32nxFwcFlightPhase;
            public int A32nxFmgcFlightPhase;
            public int A32nxAutobrakesMode;
            public bool A32nxSpoilersArmed;

            // A32NX FMA (autoflight mode annunciator) -- match values in FBW's autopilot.ts.
            // Used to render the top-of-PFD FMA banner and detect unexpected SRS engagement.
            public int A32nxFmaVerticalMode;
            public int A32nxFmaLateralMode;
            public int A32nxFmaVerticalArmed;
            public int A32nxFmaLateralArmed;

            // "T/D REACHED" PFD message -- raised by FBW VNAV when crossing top of descent.
            // Used by the DescentGuard agent to auto-start the descent.
            public int A32nxTdReached;

            // A/THR (autothrust) mode + status -- powers the leftmost FMA column.
            // Mode enum mirrors AutoThrustMode in fbw-a32nx/src/systems/shared/src/autopilot.ts.
            // Status: 0=disengaged, 1=armed, 2=active.
            public int A32nxAutothrustMode;
            public int A32nxAutothrustStatus;

            // ARINC429 discrete words that the FBW / Headwind WASM populates. We need
            // these because the legacy A32NX_AUTOTHRUST_MODE / A32NX_FMA_VERTICAL_MODE
            // shim LVars don't expose every mode (e.g. MACH, SPEED, ALT CRZ). We extract
            // individual bits in A32nxFmaModes. Read as FLOAT64 — the WASM stores the
            // raw 32-bit ARINC429 word as a double via implicit cast.
            public double A32nxAtsFmaDiscreteWord;
            public double A32nxFmgcDiscreteWord1;

            // Per-channel AP engagement (the generic SimConnect AUTOPILOT MASTER is a
            // single bit; FBW reports AP1 / AP2 separately).
            public int A32nxAp1Active;
            public int A32nxAp2Active;
        }
    }
}
