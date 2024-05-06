using Joyn.DokRouter.Common.Payloads;
using Joyn.DokRouter;
using Microsoft.AspNetCore.Mvc;

namespace Joyn.DokRouterServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DokRouterController : ControllerBase
    {
        [HttpPost("StartPipeline")]
        public IActionResult StartPipeline(StartPipeline startPipelinePayload)
        {
            MainEngine.StartPipeline(startPipelinePayload);
            return Ok();
        }

        [HttpPost("StartActivity")]
        public IActionResult StartActivity(StartActivityIn startActivityPayload)
        {
            MainEngine.StartActivity(startActivityPayload);
            return Ok();
        }

        [HttpPost("EndActivity")]
        public IActionResult EndActivity(EndActivity endActivityPayload)
        {
            MainEngine.EndActivity(endActivityPayload);
            return Ok();
        }
    }
}
