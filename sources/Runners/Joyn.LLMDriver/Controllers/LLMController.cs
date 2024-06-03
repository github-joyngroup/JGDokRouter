using Joyn.DokRouter.Common;
using Joyn.DokRouter.Common.Payloads;
using Joyn.LLMDriver.HelperWorkers;
using Joyn.LLMDriver.Models;
using Microsoft.AspNetCore.Mvc;

namespace Joyn.LLMDriver.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LLMController : ControllerBase
    {
        private readonly ILogger<FileController> _logger;

        public LLMController(ILogger<FileController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Entry point for classification using LLM
        /// </summary>
        [HttpPost("ClassifyUsingLLM")]
        public IActionResult ClassifyUsingLLM(StartActivityOut startActivityPayload)
        {
            //Do something async
            Task.Run(() =>
            {
                var model = ProtoBufSerializer.Deserialize<ActivityModel>(startActivityPayload.MarshalledExternalData);

                if (!startActivityPayload.TestMode)
                {
                    _logger.LogInformation($"Executing ClassifyUsingLLM for: {model.TransactionIdentifier}");
                    LLMWorker.ClassifyUsingLLM(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier);
                    _logger.LogInformation($"Executed ClassifyUsingLLM for: {model.TransactionIdentifier}");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model));
            });

            return Ok();
        }

        /// <summary>
        /// Entry point for extraction using LLM
        /// </summary>
        [HttpPost("PerformLLMExtraction")]
        public IActionResult PerformLLMExtraction(StartActivityOut startActivityPayload)
        {
            //Do something async
            Task.Run(() =>
            {
                var model = ProtoBufSerializer.Deserialize<ActivityModel>(startActivityPayload.MarshalledExternalData);

                if (!startActivityPayload.TestMode)
                {
                    _logger.LogInformation($"Executing PerformLLMExtraction for: {model.TransactionIdentifier}");
                    LLMWorker.PerformLLMExtraction(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier);
                    _logger.LogInformation($"Executed PerformLLMExtraction for: {model.TransactionIdentifier}");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model));
            });

            return Ok();
        }
    }
}