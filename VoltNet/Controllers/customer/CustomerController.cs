using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace VoltNet.Controllers.customer;

[Authorize(Roles = "Customer")]
public class CustomerController : Controller
{
    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue("UserId")!);
    }

    [HttpGet("customer/dashboard")]
    public IActionResult Dashboard()
    {
        return View("~/Views/customer/Dashboard.cshtml");
    }
}
