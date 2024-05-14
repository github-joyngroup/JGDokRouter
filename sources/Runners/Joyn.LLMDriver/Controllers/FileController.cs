using Joyn.DokRouter.Common;
using Joyn.DokRouter.Common.Payloads;
using Joyn.LLMDriver.HelperWorkers;
using Joyn.LLMDriver.Models;
using Microsoft.AspNetCore.Mvc;

namespace Joyn.LLMDriver.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FileController : ControllerBase
    {
        private readonly ILogger<FileController> _logger;

        public FileController(ILogger<FileController> logger)
        {
            _logger = logger;
        }

        [HttpPost("ProduceMetadata")]
        public IActionResult ProduceMetadata(StartActivityOut startActivityPayload)
        {
            //Do something async
            Task.Run(() =>
            {
                var model = ProtoBufSerializer.Deserialize<ActivityModel>(startActivityPayload.MarshalledExternalData);

                if (!startActivityPayload.TestMode)
                {
                    _logger.LogInformation($"Executing File.ProduceMetadata for: {model.TransactionIdentifier}");
                    FileWorker.ProduceMetadata(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier);
                    _logger.LogInformation($"Executed File.ProduceMetadata for: {model.TransactionIdentifier}");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model), true, String.Empty);
            });

            return Ok();
        }
    }
}
