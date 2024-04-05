using Microsoft.AspNetCore.Mvc;

namespace DokRouterClientTester.Controllers
{
    public class ActivityController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
