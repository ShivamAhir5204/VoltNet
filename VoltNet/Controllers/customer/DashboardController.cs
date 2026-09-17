using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VoltNet.Controllers.customer;

[Authorize(Roles = "Customer")]
[Route("customer/[controller]")]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/customer/dashboard/Index.cshtml");
    }
}
