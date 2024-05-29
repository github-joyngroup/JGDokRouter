using Joyn.DokRouter.Common;
using Joyn.DokRouter.Common.Payloads;
using Joyn.LLMDriver.HelperWorkers;
using Joyn.LLMDriver.Models;
using Microsoft.AspNetCore.Mvc;

namespace Joyn.LLMDriver.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ResumatorController : ControllerBase
    {
        private readonly ILogger<FileController> _logger;

        public ResumatorController(ILogger<FileController> logger)
        {
            _logger = logger;
        }

        ///// <summary>
        ///// Entry point for the Get Differencial CV List
        ///// </summary>
        //[HttpPost("GetDifferentialCVList")]
        //public IActionResult GetDifferentialCVList(StartActivityOut startActivityPayload)
        //{
        //    //Do something async
        //    Task.Run(() =>
        //    {
        //        var model = ProtoBufSerializer.Deserialize<ActivityModel>(startActivityPayload.MarshalledExternalData);

        //        if (!startActivityPayload.TestMode)
        //        {
        //            _logger.LogInformation($"Executing Resumator.GetDifferentialCVList for: {model.TransactionIdentifier}");
        //            ResumatorWorker.GetDifferentialCVList(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier);
        //            _logger.LogInformation($"Executed Resumator.GetDifferentialCVList for: {model.TransactionIdentifier}");
        //        }

        //        Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model), true, String.Empty);
        //    });

        //    return Ok();
        //}

        ///// <summary>
        ///// Entry point for the Get Differencial CV List
        ///// </summary>
        //[HttpPost("TestGetDifferentialCVList")]
        //public IActionResult TestGetDifferentialCVList()
        //{
        //    ResumatorWorker.GetDifferentialCVList(new ActivityModel()
        //    {
        //        BaseAssetsFilePath = "Test",
        //        CompanyIdentifier = "Growin",
        //        DatabaseIdentifier = "7f4fdd38-1c60-4295-a224-7d816e36d5cc",
        //        DomainIdentifier = Guid.NewGuid(),
        //        TransactionIdentifier = Guid.NewGuid()
        //    }, Guid.NewGuid());

        //    return Ok();
        //}
    }
}
