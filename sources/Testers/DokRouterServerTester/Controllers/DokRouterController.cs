using Microsoft.AspNetCore.Mvc;

namespace DokRouterServerTester.Controllers
{
    public class DokRouterController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
