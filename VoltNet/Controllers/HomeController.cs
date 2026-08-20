using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VoltNet.Models;

namespace VoltNet.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Stations()
    {
        return View();
    }

    public IActionResult Services()
    {
        return View();
    }

    public IActionResult Pricing()
    {
        return View();
    }

    public IActionResult Faq()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
