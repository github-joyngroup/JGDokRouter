using Joyn.DokRouter.Common.Payloads;
using Joyn.DokRouter;
using Microsoft.AspNetCore.Mvc;

namespace Joyn.DokRouterServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HomeController : ControllerBase
    {
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return Content($"I am alive: {DateTime.UtcNow.ToString("u")}");
        }

        [HttpGet("/")]
        public IActionResult Root()
        {
            return RedirectPermanent("/Home/Index");
        }
    }
}