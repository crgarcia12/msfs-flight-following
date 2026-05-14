using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MSFSFlightFollowing.SimConnect;

namespace MSFSFlightFollowing.Controllers;

/// <summary>
/// HTTP surface for the web FCU panel. Every endpoint maps to a single FCU command on
/// <see cref="ISimCommands"/>. Each command transmits both A32NX.* and A339X.* events
/// so the same call works for the FlyByWire A32NX and the Headwind A330-900.
/// </summary>
[ApiController]
[Route("api/fcu")]
public sealed class FcuController : ControllerBase
{
    private readonly ISimCommands _sim;

    public FcuController(ISimCommands sim)
    {
        _sim = sim;
    }

    public sealed record ValueRequest(int Value);

    // ---- SPD ----
    [HttpPost("spd/set")]  public IActionResult SetSpeed([FromBody] ValueRequest req) { _sim.FcuSetSpeed(req.Value); return Ok(); }
    [HttpPost("spd/push")] public IActionResult PushSpeed() { _sim.FcuPushSpeed(); return Ok(); }
    [HttpPost("spd/pull")] public IActionResult PullSpeed() { _sim.FcuPullSpeed(); return Ok(); }

    // ---- HDG ----
    [HttpPost("hdg/set")]  public IActionResult SetHeading([FromBody] ValueRequest req) { _sim.FcuSetHeading(req.Value); return Ok(); }
    [HttpPost("hdg/push")] public IActionResult PushHeading() { _sim.FcuPushHeading(); return Ok(); }
    [HttpPost("hdg/pull")] public IActionResult PullHeading() { _sim.FcuPullHeading(); return Ok(); }

    // ---- ALT ----
    [HttpPost("alt/set")]  public IActionResult SetAltitude([FromBody] ValueRequest req) { _sim.FcuSetAltitude(req.Value); return Ok(); }
    [HttpPost("alt/push")] public IActionResult PushAltitude() { _sim.FcuPushAltitude(); return Ok(); }
    [HttpPost("alt/pull")] public IActionResult PullAltitude() { _sim.FcuPullAltitude(); return Ok(); }

    // ---- V/S ----
    [HttpPost("vs/set")]   public IActionResult SetVs([FromBody] ValueRequest req) { _sim.FcuSetVerticalSpeed(req.Value); return Ok(); }
    [HttpPost("vs/push")]  public IActionResult PushVs() { _sim.FcuPushVerticalSpeed(); return Ok(); }
    [HttpPost("vs/pull")]  public IActionResult PullVs() { _sim.FcuPullVerticalSpeed(); return Ok(); }

    // ---- Buttons ----
    [HttpPost("ap1")]   public IActionResult Ap1()   { _sim.FcuToggleAp1();   return Ok(); }
    [HttpPost("ap2")]   public IActionResult Ap2()   { _sim.FcuToggleAp2();   return Ok(); }
    [HttpPost("athr")]  public IActionResult Athr()  { _sim.FcuToggleAthr();  return Ok(); }
    [HttpPost("loc")]   public IActionResult Loc()   { _sim.FcuPushLoc();     return Ok(); }
    [HttpPost("appr")]  public IActionResult Appr()  { _sim.FcuPushAppr();    return Ok(); }
    [HttpPost("exped")] public IActionResult Exped() { _sim.FcuPushExped();   return Ok(); }
}
