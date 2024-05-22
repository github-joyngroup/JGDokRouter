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

        /// <summary>
        /// Entry point for the metadata production activity
        /// </summary>
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

        /// <summary>
        /// Entry point for the Produce Images activity
        /// </summary>
        [HttpPost("ProduceImages")]
        public IActionResult ProduceImages(StartActivityOut startActivityPayload)
        {
            //Do something async
            Task.Run(() =>
            {

                var model = ProtoBufSerializer.Deserialize<ActivityModel>(startActivityPayload.MarshalledExternalData);

                if (!startActivityPayload.TestMode)
                {
                    _logger.LogInformation($"Executing ProduceImages for: {model.TransactionIdentifier}");
                    FileWorker.ProduceImages(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier);
                    _logger.LogInformation($"Executed ProduceImages for: {model.TransactionIdentifier}");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model), true, String.Empty);
            });

            return Ok();
        }

        /// <summary>
        /// Entry point for the Produce OCR Assets activity
        /// </summary>
        [HttpPost("ProduceOCRAssets")]
        public IActionResult ProduceOCRAssets(StartActivityOut startActivityPayload)
        {
            //Do something async
            Task.Run(() =>
            {
                var model = ProtoBufSerializer.Deserialize<ActivityModel>(startActivityPayload.MarshalledExternalData);

                if (!startActivityPayload.TestMode)
                {
                    _logger.LogInformation($"Executing ProduceOCRAssets for: {model.TransactionIdentifier}");
                    FileWorker.ProduceOCRAssets(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier);
                    _logger.LogInformation($"Executed ProduceOCRAssets for: {model.TransactionIdentifier}");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model), true, String.Empty);
            });

            return Ok();
        }

        /// <summary>
        /// Entry point for the Consolidate Assets activity
        /// </summary>
        [HttpPost("ConsolidateAssets")]
        public IActionResult ConsolidateAssets(StartActivityOut startActivityPayload)
        {
            //Do something async
            Task.Run(() =>
            {
                var model = ProtoBufSerializer.Deserialize<ActivityModel>(startActivityPayload.MarshalledExternalData);

                if (!startActivityPayload.TestMode)
                {
                    _logger.LogInformation($"Executing ConsolidateAssets for: {model.TransactionIdentifier}");
                    FileWorker.ConsolidateAssets(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier);
                    _logger.LogInformation($"Executed ConsolidateAssets for: {model.TransactionIdentifier}");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model), true, String.Empty);
            });

            return Ok();
        }
    }
}
