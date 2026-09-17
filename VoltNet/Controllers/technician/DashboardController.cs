using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VoltNet.Controllers.technician;

[Authorize(Roles = "Technician")]
[Route("technician/[controller]")]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/technician/dashboard/Index.cshtml");
    }
}
