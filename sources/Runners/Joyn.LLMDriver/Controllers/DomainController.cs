using DocDigitizer.Common.Logging;
using Google.Protobuf;
using Joyn.DokRouter.Common;
using Joyn.DokRouter.Common.Payloads;
using Joyn.LLMDriver.DAL;
using Joyn.LLMDriver.HelperWorkers;
using Joyn.LLMDriver.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace Joyn.LLMDriver.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DomainController : ControllerBase
    {
        private const string RequestDomainIdKey = "DomainId";
        private const string RequestTransactionIdKey = "TransactionId";
        private static Guid DefaultDomainIdentifierForStartFromDokRouter = new Guid("8d93b022-c6a5-4e5e-9cde-02269de744c5");

        private static DomainControllerConfiguration _configuration;

        public static void Startup(DomainControllerConfiguration configuration)
        {
            _configuration = configuration;            
        }

        [HttpPost("Setup")]
        public IActionResult Setup()
        {
            return Content($"TO DO: SETUP DOMAIN");
        }

        [HttpDelete("Delete")]
        public IActionResult Delete()
        {
            return Content($"TO DO: DELETE DOMAIN");
        }

        [HttpPost("StartFromDokRouter")]
        public IActionResult StartFromDokRouter(StartActivityOut startActivityPayload)
        {
            //Do something async
            Task.Run(() =>
            {
                ActivityModel activityModel = null;

                if (!startActivityPayload.TestMode)
                {
                    DDLogger.LogInfo<DomainController>($"Executing StartFromDokRouter");

                    //TODO: How to obtain the domain identifier?
                    activityModel = DomainWorker.StartPipelineByDomain(null, DefaultDomainIdentifierForStartFromDokRouter, null, String.Empty, null, null);

                    DDLogger.LogInfo<DomainController>($"Executed StartFromDokRouter");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(activityModel), true, String.Empty);
            });

            return Ok();
        }

        [HttpPost("Start")]
        public IActionResult Start()
        {
            if (!Request.HasFormContentType) { return BadRequest("Invalid Request, no Form Data to extract payload from. Check "); }
            var formKeys = Request.Form.Keys;

            if(!formKeys.Contains(RequestDomainIdKey)) { return BadRequest("DomainId is required"); }
            if(Request.Form.Files.Count > 1) { return BadRequest("Please Post one File at a time."); }
            if(Request.Form.Files.Count == 1)
            {
                var extension = Path.GetExtension(Request.Form.Files.First().FileName)?.ToLower();
                if (extension != ".pdf") { throw new Exception("Invalid File Type - Please only upload PDF files"); }
            }
            
            if (!Guid.TryParse(Request.Form[RequestDomainIdKey], out Guid domainIdentifier)) { return BadRequest("DomainId is not a valid Guid"); }
            Guid.TryParse(Request.Form[RequestTransactionIdKey], out Guid transactionIdentifier);

            Dictionary<string, string> startPayload = new Dictionary<string, string>();
            foreach (var key in formKeys)
            {
                startPayload.Add(key, Request.Form[key]);
            }

            //If a file is received, save it to the assets folder
            IFormFile uploadedFile = null;
            if (Request.Form.Files.Any())
            {
                uploadedFile = Request.Form.Files.First();
            }

            var activityModel = DomainWorker.StartPipelineByDomain(transactionIdentifier == Guid.Empty ? null : transactionIdentifier, domainIdentifier, _configuration.LLMDriverPipelineIdentifier, String.Empty, startPayload, uploadedFile);

            return new JsonResult(new { success = true, transactionIdentifier = activityModel.TransactionIdentifier });
        }
    }

    public class DomainControllerConfiguration
    {
        //TODO: Remove this PipelineIdentifier should be configured in domain configuration
        public Guid LLMDriverPipelineIdentifier { get; set; }
    }
}
