using static MSFSFlightFollowing.Models.SimConnectStructs;

namespace MSFSFlightFollowing.Models
{
   public class AircraftStatusModel
   {
      public class AutoPilot
      {
         public bool Available { get; set; }
         public bool Master { get; set; }
         public bool Level { get; set; }
         public bool Altitude { get; set; }
         public bool Approach { get; set; }
         public bool Backcourse { get; set; }
         public bool FlightDirector { get; set; }
         public bool Airspeed { get; set; }
         public bool Mach { get; set; }
         public bool YawDamper { get; set; }
         public bool Autothrottle { get; set; }
         public bool VerticalHold { get; set; }
         public bool Heading { get; set; }
         public bool Nav1 { get; set; }
      }

      public double Latitude { get; set; }
      public double Longitude { get; set; }
      public double Altitude { get; set; }
      public double VerticalSpeedFpm { get; set; }
      public string FlightPhase { get; set; } = "Preflight";
      public double TotalFuel { get; set; }
      public double CurrentFuel { get; set; }
      public double TrueHeading { get; set; }
      public double AirspeedIndicated { get; set; }
      public double AirspeedTrue { get; set; }
      public bool NavHasSignal { get; set; }
      public bool NavHasDME { get; set; }
      public double DMEDistance { get; set; }
      public bool GPSFlightPlanActive { get; set; }
      public bool GPSWaypointModeActive { get; set; }
      public int GPSWaypointIndex { get; set; }
      public double GPSWaypointDistance { get; set; }
      public double GPSNextWPLatitude { get; set; }
      public double GPSNextWPLongitude { get; set; }
      public double GPSPrevWPLatitude { get; set; }
      public double GPSPrevWPLongitude { get; set; }
      public double GPSWPETE { get; set; }

      // Attitude / dynamics
      public double PitchDegrees { get; set; }
      public double BankDegrees { get; set; }
      public double GroundSpeedKnots { get; set; }
      public double GForce { get; set; }

      // Environment
      public double WindVelocityKnots { get; set; }
      public double WindDirectionDegrees { get; set; }
      public double OutsideAirTempC { get; set; }

      // Radio altimeter / ground
      public double RadioAltitudeFeet { get; set; }
      public bool OnGround { get; set; }

      // Configuration
      public int FlapsHandleIndex { get; set; }

      // FlyByWire A32NX-specific (0/false when the FBW module is not running)
      public int A32nxThrustLimitType { get; set; }
      public int A32nxFwcFlightPhase { get; set; }
      public int A32nxFmgcFlightPhase { get; set; }
      public int A32nxAutobrakesMode { get; set; }
      public bool A32nxSpoilersArmed { get; set; }
      public bool IsA32nx => A32nxFwcFlightPhase > 0;

      // FlyByWire A32NX FMA — raw integers + decoded labels for the front-end.
      public int A32nxFmaVerticalMode { get; set; }
      public int A32nxFmaLateralMode { get; set; }
      public int A32nxFmaVerticalArmed { get; set; }
      public int A32nxFmaLateralArmed { get; set; }
      public string FmaPitch { get; set; } = "";
      public string FmaRoll { get; set; } = "";
      public string FmaPitchArmed { get; set; } = "";
      public string FmaRollArmed { get; set; } = "";
      public bool FmaIsSrs { get; set; }

      /// <summary>True while the "T/D REACHED" message is shown on the PFD.
      /// Set by the FBW VNAV component on the A32NX (and inherited by Headwind A330).</summary>
      public bool A32nxTdReached { get; set; }

      // ---- A/THR (autothrust) — leftmost FMA column on Airbus PFD ----
      public int A32nxAutothrustMode { get; set; }
      public int A32nxAutothrustStatus { get; set; }
      /// <summary>Label rendered in the leftmost FMA column (e.g. "THR CLB", "SPEED").</summary>
      public string FmaAthr { get; set; } = "";
      /// <summary>True while A/THR is active and driving the thrust levers.</summary>
      public bool FmaAthrActive { get; set; }
      /// <summary>True while A/THR is armed but not yet active (e.g. before takeoff).</summary>
      public bool FmaAthrArmed { get; set; }

      // ---- ARINC429 packed words from the FBW WASM. We decode bits in A32nxFmaModes
      // to recover the FMA modes the legacy integer LVars don't expose (MACH, SPEED,
      // ALT CRZ, etc.). Stored as double because the WASM writes the raw 32-bit
      // word as a FLOAT64. ----
      public double A32nxAtsFmaDiscreteWord { get; set; }
      public double A32nxFmgcDiscreteWord1 { get; set; }

      // ---- Per-channel AP engagement (FBW reports AP1 / AP2 independently) ----
      public bool A32nxAp1 { get; set; }
      public bool A32nxAp2 { get; set; }

      public AutoPilot Autopilot { get; set; }

      public AircraftStatusModel(AircraftStatusStruct status)
      {
         Latitude = status.Latitude;
         Longitude = status.Longitude;
         Altitude = status.Altitude;
         TotalFuel = status.TotalFuel;
         CurrentFuel = status.CurrentFuel;
         TrueHeading = status.TrueHeading;
         AirspeedIndicated = status.AirspeedIndicated;
         AirspeedTrue = status.AirspeedTrue;

         NavHasSignal = status.NavHasSignal;
         NavHasDME = status.NavHasDME;
         DMEDistance = status.DMEDistance;
         GPSFlightPlanActive = status.GPSFlightPlanActive;
         GPSWaypointModeActive = status.GPSWaypointModeActive;
         GPSWaypointIndex = status.GPSWaypointIndex;
         GPSWaypointDistance = status.GPSWaypointDistance;
         GPSNextWPLatitude = status.GPSNextWPLatitude;
         GPSNextWPLongitude = status.GPSNextWPLongitude;
         GPSPrevWPLatitude = status.GPSPrevWPLatitude;
         GPSPrevWPLongitude = status.GPSPrevWPLongitude;
         GPSWPETE = status.GPSWPETE;

         PitchDegrees = status.PitchDegrees;
         BankDegrees = status.BankDegrees;
         GroundSpeedKnots = status.GroundSpeedKnots;
         GForce = status.GForce;
         WindVelocityKnots = status.WindVelocityKnots;
         WindDirectionDegrees = status.WindDirectionDegrees;
         OutsideAirTempC = status.OutsideAirTempC;
         RadioAltitudeFeet = status.RadioAltitudeFeet;
         OnGround = status.OnGround;
         FlapsHandleIndex = status.FlapsHandleIndex;
         A32nxThrustLimitType = status.A32nxThrustLimitType;
         A32nxFwcFlightPhase = status.A32nxFwcFlightPhase;
         A32nxFmgcFlightPhase = status.A32nxFmgcFlightPhase;
         A32nxAutobrakesMode = status.A32nxAutobrakesMode;
         A32nxSpoilersArmed = status.A32nxSpoilersArmed;

         A32nxFmaVerticalMode  = status.A32nxFmaVerticalMode;
         A32nxFmaLateralMode   = status.A32nxFmaLateralMode;
         A32nxFmaVerticalArmed = status.A32nxFmaVerticalArmed;
         A32nxFmaLateralArmed  = status.A32nxFmaLateralArmed;
         A32nxAtsFmaDiscreteWord = status.A32nxAtsFmaDiscreteWord;
         A32nxFmgcDiscreteWord1  = status.A32nxFmgcDiscreteWord1;
         FmaPitch       = MSFSFlightFollowing.SimConnect.A32nxFmaModes.VerticalLabel(A32nxFmaVerticalMode, A32nxFmgcDiscreteWord1);
         FmaRoll        = MSFSFlightFollowing.SimConnect.A32nxFmaModes.LateralLabel(A32nxFmaLateralMode);
         FmaPitchArmed  = MSFSFlightFollowing.SimConnect.A32nxFmaModes.VerticalArmedLabel(A32nxFmaVerticalArmed);
         FmaRollArmed   = MSFSFlightFollowing.SimConnect.A32nxFmaModes.LateralArmedLabel(A32nxFmaLateralArmed);
         FmaIsSrs       = MSFSFlightFollowing.SimConnect.A32nxFmaModes.IsSrs(A32nxFmaVerticalMode);

         A32nxTdReached = status.A32nxTdReached != 0;

         A32nxAutothrustMode   = status.A32nxAutothrustMode;
         A32nxAutothrustStatus = status.A32nxAutothrustStatus;
         FmaAthr        = MSFSFlightFollowing.SimConnect.A32nxFmaModes.AutothrustLabel(A32nxAutothrustMode, A32nxAtsFmaDiscreteWord);
         FmaAthrActive  = MSFSFlightFollowing.SimConnect.A32nxFmaModes.IsAthrActive(A32nxAutothrustStatus);
         FmaAthrArmed   = MSFSFlightFollowing.SimConnect.A32nxFmaModes.IsAthrArmed(A32nxAutothrustStatus);

         A32nxAp1 = status.A32nxAp1Active != 0;
         A32nxAp2 = status.A32nxAp2Active != 0;

         Autopilot = new AutoPilot()
         {
            Available = status.AutopilotAvailable,
            Master = status.AutopilotMaster,
            FlightDirector = status.AutopilotFlightDirector,
            Airspeed = status.AutopilotAirspeed,
            Altitude = status.AutopilotAltitude,
            Approach = status.AutopilotApproach,
            Autothrottle = status.AutopilotAutothrottle,
            Backcourse = status.AutopilotBackcourse,
            Heading = status.AutopilotHeading,
            Level = status.AutopilotWingLevel,
            Mach = status.AutopilotMach,
            Nav1 = status.AutopilotNav1,
            VerticalHold = status.AutopilotVerticalHold,
            YawDamper = status.AutopilotYawDamper
         };
      }

   }
}
