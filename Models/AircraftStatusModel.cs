using System.Text.Json.Serialization;
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

      /// <summary>
      /// Altimeter QNH in hPa (millibars). Mirrors the Kohlsman setting on the
      /// pilot-side altimeter so the web UI can show the same QNH as the
      /// cockpit and offer a one-tap "sync to local QNH" (B-key equivalent).
      /// </summary>
      public int QnhMb { get; set; }

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

      /// <summary>
      /// Pre-formatted FCU window contents, mirroring the cockpit Airbus FCU.
      /// `Source = "fbw"` when FBW/Headwind LVars are populated; "generic" when
      /// only stock SimConnect AP targets are available; "none" before sim load.
      /// JSON name `fcuDisplay` to avoid clashing with the existing front-end
      /// `fcu` object that holds the user-typed FCU control inputs.
      /// </summary>
      [JsonPropertyName("fcuDisplay")]
      public FcuDisplay Fcu { get; set; } = new FcuDisplay();

      public class FcuDisplay
      {
         public string Source { get; set; } = "none"; // "fbw" | "generic" | "none"

         // Pre-formatted strings — render these directly in the FCU windows.
         public string SpdText { get; set; } = "---";
         public string HdgText { get; set; } = "---";
         public string AltText { get; set; } = "-----";
         public string VsText  { get; set; } = "-----";

         // Mode hints for the small column labels above each window.
         public bool   SpdManaged { get; set; }
         public bool   SpdIsMach  { get; set; }
         public bool   HdgManaged { get; set; }
         public bool   HdgIsTrack { get; set; }
         public bool   AltManaged { get; set; }
         public bool   VsActive   { get; set; }   // window shows a value (V/S or FPA mode engaged)
         public bool   VsIsFpa    { get; set; }
      }

      /// <summary>
      /// Debug-only mirror of the raw SimConnect struct. Hidden from the
      /// SignalR/JSON payload via [JsonIgnore]; the /api/debug/snapshot endpoint
      /// reaches into this to surface raw LVar values when troubleshooting.
      /// </summary>
      [JsonIgnore]
      public AircraftStatusStruct RawStruct { get; private set; }

      public AircraftStatusModel(AircraftStatusStruct status)
      {
         RawStruct = status;
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
         QnhMb = (int)System.Math.Round(status.QnhMb);
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

         Fcu = BuildFcuDisplay(status);

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

      // ----------------------------------------------------------------------
      // FCU display formatter — turns the raw FBW LVars (or stock SimConnect
      // AP target vars) into the strings the cockpit FCU windows would show.
      // Format is shared by desktop & mobile so both views stay consistent.
      // ----------------------------------------------------------------------
      private static FcuDisplay BuildFcuDisplay(AircraftStatusStruct s)
      {
         var d = new FcuDisplay();

         bool isFbw = s.A32nxFwcFlightPhase > 0
                      || s.FcuSpeedSelected != 0
                      || s.FcuAltitudeSelected != 0
                      || s.FcuHeadingSelected != 0
                      || s.FcuMachSelected != 0
                      || s.FcuSpdManagedDashes != 0
                      || s.FcuHdgManagedDashes != 0;

         if (isFbw)
         {
            d.Source = "fbw";

            // ---- SPD/MACH window ----
            d.SpdManaged = s.FcuSpdManagedDot != 0;
            bool spdDashes = s.FcuSpdManagedDashes != 0;
            // FBW returns -1 (or sometimes 100) in SPEED_SELECTED when mach is the
            // active reference. Use the relative validity of the two LVars to pick.
            bool machIsRef = s.FcuMachSelected > 0
                             && (s.FcuSpeedSelected <= 0 || s.FcuSpeedSelected > 999);
            d.SpdIsMach = machIsRef;
            // Selected speed falls back to stock SimConnect when the FBW LVar
            // is not populated (Headwind A330 renames some FBW LVars).
            double spdVal = s.FcuSpeedSelected > 0 ? s.FcuSpeedSelected
                          : (s.ApAirspeedHoldVar > 0 ? s.ApAirspeedHoldVar : 0);
            if (spdDashes)                d.SpdText = "---";
            else if (machIsRef)           d.SpdText = "." + ((int)System.Math.Round(s.FcuMachSelected * 100)).ToString("D2");
            else if (spdVal > 0)          d.SpdText = ((int)System.Math.Round(spdVal)).ToString("D3");
            else                          d.SpdText = "---";

            // ---- HDG/TRK window ----
            d.HdgManaged = s.FcuHdgManagedDot != 0;
            d.HdgIsTrack = s.FcuTrkFpaModeActive != 0;
            bool hdgDashes = s.FcuHdgManagedDashes != 0;
            // Prefer DISPLAY_HDG_TRK_VALUE (already-formatted FBW WASM output),
            // then fall back to HEADING_SELECTED, then stock SimConnect.
            int hdgVal = (int)s.DISPLAY_HDG_TRK;
            if (hdgVal <= 0 || hdgVal > 360)
                hdgVal = (int)System.Math.Round(s.FcuHeadingSelected);
            if (hdgVal <= 0 || hdgVal > 360)
                hdgVal = (int)System.Math.Round(s.ApHeadingLockDir);
            hdgVal = ((hdgVal % 360) + 360) % 360;
            if (hdgVal == 0) hdgVal = 360; // Airbus shows 360 not 000
            d.HdgText = hdgDashes ? "---" : hdgVal.ToString("D3");

            // ---- ALT window ----
            d.AltManaged = s.FcuAltManaged != 0;
            // FBW's A32NX_AUTOPILOT_ALTITUDE_SELECTED returns 0 on Headwind A330
            // (renamed in the fork). Fall back to the stock SimConnect AP target,
            // which is always the same value that's shown in the FCU window.
            int altFt = (int)System.Math.Round(s.FcuAltitudeSelected);
            if (altFt <= 0) altFt = (int)System.Math.Round(s.ApAltitudeLockVar);
            d.AltText = altFt > 0 ? altFt.ToString("D5") : "-----";

            // ---- V/S - FPA window ----
            // Window shows a value only when the pilot has pulled V/S or FPA,
            // i.e. the FMA vertical mode is V/S (14) or FPA (15) on FBW.
            bool vsMode  = s.A32nxFmaVerticalMode == 14;
            bool fpaMode = s.A32nxFmaVerticalMode == 15;
            d.VsActive = vsMode || fpaMode;
            d.VsIsFpa  = fpaMode || s.FcuTrkFpaModeActive != 0;
            if (fpaMode)
            {
               double fpa = s.FcuFpaSelected;
               string sign = fpa >= 0 ? "+" : "-";
               d.VsText = sign + System.Math.Abs(fpa).ToString("F1") + "°";
            }
            else if (vsMode)
            {
               int vs = (int)System.Math.Round(s.FcuVsSelected);
               // Same FBW-renamed-LVar fallback as ALT.
               if (vs == 0 && s.ApVerticalHoldVar != 0)
                  vs = (int)System.Math.Round(s.ApVerticalHoldVar);
               string sign = vs >= 0 ? "+" : "-";
               d.VsText = sign + System.Math.Abs(vs).ToString("D4");
            }
            else d.VsText = "-----";
         }
         else if (s.AutopilotMaster || s.AutopilotAvailable)
         {
            // Generic fallback — stock SimConnect AP target vars. No managed dots.
            d.Source = "generic";
            int spd = (int)System.Math.Round(s.ApAirspeedHoldVar);
            d.SpdText = spd > 0 ? spd.ToString("D3") : "---";
            int hdg = (int)System.Math.Round(s.ApHeadingLockDir);
            hdg = ((hdg % 360) + 360) % 360;
            if (hdg == 0) hdg = 360;
            d.HdgText = hdg.ToString("D3");
            int alt = (int)System.Math.Round(s.ApAltitudeLockVar);
            d.AltText = alt > 0 ? alt.ToString("D5") : "-----";
            int vs = (int)System.Math.Round(s.ApVerticalHoldVar);
            d.VsActive = s.AutopilotVerticalHold;
            if (s.AutopilotVerticalHold)
            {
               string sign = vs >= 0 ? "+" : "-";
               d.VsText = sign + System.Math.Abs(vs).ToString("D4");
            }
            else d.VsText = "-----";
         }
         // else: leave defaults ("---", source="none") — UI hides the bar.

         return d;
      }

   }
}
