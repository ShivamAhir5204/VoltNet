using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VoltNet.Controllers.stationmanager;

[Authorize(Roles = "StationManager")]
[Route("stationmanager/[controller]")]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/stationmanager/dashboard/Index.cshtml");
    }
}
