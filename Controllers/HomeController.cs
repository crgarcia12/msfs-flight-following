using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using MSFSFlightFollowing.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace MSFSFlightFollowing.Controllers
{
   public class HomeController : Controller
   {
      private readonly IWebHostEnvironment _host;
      private static List<AIRPORT> _cachedAirports;
      private static readonly object _cacheLock = new object();

      public HomeController(IWebHostEnvironment host)
      {
         _host = host;
      }

      public IActionResult Index()
      {
         return View();
      }

      /// <summary>
      /// Phone-first single-page UI for tracking the flight remotely and driving
      /// the autopilot. Reachable at <c>GET /m</c> from any device on the LAN.
      /// Shares the SignalR hub + REST endpoints with the desktop view.
      /// </summary>
      [HttpGet("/m")]
      [HttpGet("/mobile")]
      public IActionResult Mobile()
      {
         return View();
      }

      [HttpGet("get/airports/")]
      public JsonResult Airports()
      {
         lock (_cacheLock)
         {
            if (_cachedAirports == null)
            {
               var result = new List<AIRPORT>();
               var path = Path.Combine(_host.ContentRootPath, "Airports");
               if (Directory.Exists(path))
               {
                  foreach (var file in Directory.GetFiles(path))
                  {
                     using FileStream fs = System.IO.File.Open(file, FileMode.Open);
                     if (fs.Length == 0) continue;

                     var serializer = new XmlSerializer(typeof(OPENAIP));
                     var xml = (OPENAIP)serializer.Deserialize(fs);

                     foreach (var airport in xml.WAYPOINTS.AIRPORT)
                     {
                        if (airport.TYPE != "HELI_CIVIL")
                           result.Add(airport);
                     }
                  }
               }
               _cachedAirports = result;
            }
            return Json(new { data = _cachedAirports });
         }
      }

      [HttpGet("healthz")]
      public JsonResult Health([FromServices] MSFSFlightFollowing.SimConnect.SimConnector sim, [FromServices] SimBridgeClient bridge, [FromServices] FSUIPCWinformsAutoCS.EventHub hub)
      {
         return Json(new
         {
            sim = sim.IsConnected,
            simBridge = bridge.IsConnected,
            simBridgeEnabled = bridge.Enabled,
            simBridgeStatus = bridge.Status.ToString(),
            eventHubEnabled = hub.Enabled,
            simWriteEnabled = sim.WriteEnabled
         });
      }

      /// <summary>
      /// Read-only diagnostic dump of the latest SimConnect snapshot — useful for
      /// debugging FMA / autothrust LVars when the values in the cockpit don't
      /// match what the UI is showing. Returns the raw integer enum values so
      /// you can confirm what MSFS is publishing for each FBW / Headwind LVar.
      /// </summary>
      [HttpGet("api/debug/snapshot")]
      public JsonResult DebugSnapshot([FromServices] MSFSFlightFollowing.SimConnect.SimConnector sim)
      {
         var s = sim.LatestSnapshot;
         if (s == null) return Json(new { sim = false, message = "No SimConnect snapshot yet." });
         return Json(new
         {
            sim = true,
            connected = sim.IsConnected,
            isA32nx = s.IsA32nx,
            fmgcPhase = s.A32nxFmgcFlightPhase,
            verticalMode    = new { raw = s.A32nxFmaVerticalMode,  label = s.FmaPitch },
            verticalArmed   = new { raw = s.A32nxFmaVerticalArmed, label = s.FmaPitchArmed },
            lateralMode     = new { raw = s.A32nxFmaLateralMode,   label = s.FmaRoll },
            lateralArmed    = new { raw = s.A32nxFmaLateralArmed,  label = s.FmaRollArmed },
            autothrustMode  = new { raw = s.A32nxAutothrustMode,   label = s.FmaAthr },
            autothrustStatus = s.A32nxAutothrustStatus,
            atsFmaDiscreteWordRaw = s.A32nxAtsFmaDiscreteWord,
            atsFmaDiscreteWordHex = ((uint)s.A32nxAtsFmaDiscreteWord).ToString("X8"),
            fmgcDiscreteWord1Raw  = s.A32nxFmgcDiscreteWord1,
            fmgcDiscreteWord1Hex  = ((uint)s.A32nxFmgcDiscreteWord1).ToString("X8"),
            ap1 = s.A32nxAp1,
            ap2 = s.A32nxAp2,
            tdReached = s.A32nxTdReached,
            altitudeFt = (int)s.Altitude,
            ias = (int)s.AirspeedIndicated,
            hdg = (int)s.TrueHeading
         });
      }

      /// <summary>
      /// Captures the current altitude on the A32NX FCU and engages ALT hold.
      /// Wired to the red RECOVER ALT button on the HUD; safe to call any time but
      /// only meaningful with a FlyByWire A32NX loaded.
      /// </summary>
      [HttpPost("api/sim/recover-alt")]
      public async System.Threading.Tasks.Task<JsonResult> RecoverAltitude(
         [FromServices] MSFSFlightFollowing.Agents.AltitudeGuard guard)
      {
         await guard.RecoverAltitudeNow();
         return Json(new { ok = true });
      }

      /// <summary>
      /// Manually fires the auto-descent: sets the FCU ALT to <c>value</c> (or the
      /// configured default if omitted) and pushes ALT for managed descent.
      /// Used by the "BEGIN DESCENT" button on the HUD / mobile views.
      /// </summary>
      [HttpPost("api/sim/begin-descent")]
      public async System.Threading.Tasks.Task<JsonResult> BeginDescent(
         [FromServices] MSFSFlightFollowing.Agents.DescentGuard guard,
         [FromBody] BeginDescentRequest req)
      {
         await guard.BeginDescentNow(req?.Value);
         return Json(new { ok = true });
      }

      public sealed record BeginDescentRequest(int? Value);
   }
}
