using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VoltNet.Controllers.stationowner;

[Authorize(Roles = "StationOwner")]
[Route("stationowner/[controller]")]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/stationowner/dashboard/Index.cshtml");
    }
}
