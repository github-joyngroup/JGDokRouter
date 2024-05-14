using DocDigitizer.Common.Logging;
using Google.Protobuf;
using Joyn.DokRouter.Common;
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

            //TODO: Check if domain exists

            //Generate transaction identifier
            Guid transactionIdentifier = Guid.NewGuid();

            //Create working directory
            var processFolder = Path.Combine(_configuration.BaseWorkingFolderPath, domainIdentifier.ToString(), "processes", transactionIdentifier.ToString());

            DDLogger.LogDebug<DomainController>($"Creating Process folder: '{processFolder}'");
            Directory.CreateDirectory(processFolder);

            string baseAssetsFilePath = Path.Combine(processFolder, "assets");
            Directory.CreateDirectory(baseAssetsFilePath);

            ActivityModel activityModel = new ActivityModel()
            {
                TransactionIdentifier = transactionIdentifier,
                DatabaseIdentifier = transactionIdentifier.ToString(),
                DomainIdentifier = domainIdentifier,
                BaseAssetsFilePath = baseAssetsFilePath,
            };

            Dictionary<string, string> startPayload = new Dictionary<string, string>();
            foreach(var key in formKeys)
            {
                startPayload.Add(key, Request.Form[key]);
            }   

            LLMProcessData llmProcessData = new LLMProcessData()
            {
                Id = transactionIdentifier.ToString(),
                CreatedAt = DateTime.UtcNow,
                //ProcessData = new Dictionary<string, string>()
                //{
                //    { LLMProcessDataConstants.ActivityModelKey, JsonSerializer.Serialize(activityModel) },
                //    { LLMProcessDataConstants.StartPayloadKey, JsonSerializer.Serialize(startPayload) }
                //}

                ProcessData = new Dictionary<string, BsonDocument>()
                {
                    { LLMProcessDataConstants.ActivityModelKey, activityModel.ToBsonDocument() },
                    { LLMProcessDataConstants.StartPayloadKey, startPayload.ToBsonDocument() }
                }
            };

            LLMProcessDataDAL.SaveOrUpdate(llmProcessData);

            //If a file is received, save it to the assets folder
            if (Request.Form.Files.Any())
            {
                var uploadedFile = Request.Form.Files.First();
                var filePath = Path.Combine(baseAssetsFilePath, "original.pdf");

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    var copyTask = uploadedFile.CopyToAsync(stream);
                    Task.WaitAll(copyTask);
                }

                llmProcessData.ProcessData[LLMProcessDataConstants.FileInformationKey] = new UploadedFileInformation()
                {
                    EnvelopeUuid = Guid.NewGuid(),
                    OriginalFileName = uploadedFile.FileName,
                    OriginalContentType = uploadedFile.ContentType,
                    LocalFilePath = filePath
                }.ToBsonDocument();

                LLMProcessDataDAL.SaveOrUpdate(llmProcessData);
            }

            //Start the pipeline
            Common.StartPipeline(_configuration.LLMDriverPipelineIdentifier, transactionIdentifier, ProtoBufSerializer.Serialize(activityModel), _configuration.DokRouterStartPipelineUrl);

            return new JsonResult(new { success = true, transactionIdentifier = transactionIdentifier });
        }
    }

    public class DomainControllerConfiguration
    {
        public string BaseWorkingFolderPath { get; set; }
        public Guid LLMDriverPipelineIdentifier { get; set; }
        public string DokRouterStartPipelineUrl { get; set; }
    }
}
