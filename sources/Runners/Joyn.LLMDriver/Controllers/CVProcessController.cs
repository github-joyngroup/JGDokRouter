using DocDigitizer.Common.Logging;
using Joyn.DokRouter.Common;
using Joyn.DokRouter.Common.Payloads;
using Joyn.LLMDriver.DAL;
using Joyn.LLMDriver.HelperWorkers;
using Joyn.LLMDriver.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Core.Clusters;

namespace Joyn.LLMDriver.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CVProcessController : ControllerBase
    {
        private readonly ILogger<FileController> _logger;

        public CVProcessController(ILogger<FileController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Entry point for the Start Resumator Synchronization
        /// </summary>
        [HttpPost("StartResumatorSynchronization")]
        public IActionResult StartResumatorSynchronization(StartActivityOut startActivityPayload)
        {
            //Do something async
            Task.Run(() =>
            {
                var model = startActivityPayload.MarshalledExternalData != null ? ProtoBufSerializer.Deserialize<ActivityModel>(startActivityPayload.MarshalledExternalData) : null;

                if (!startActivityPayload.TestMode)
                {
                    _logger.LogInformation($"Executing CVProcess.StartFormatCVProcesses for: {model.TransactionIdentifier}");
                    List<LLMCompanyData> companies = LLMCompanyDataDAL.ListCompanies();
                    foreach(var company in companies)
                    {
                        if(company.CVSynchronizationEnabled)
                        {
                            DDLogger.LogInfo<CVProcessController>($"{model.TransactionIdentifier} - {startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier} - Start Update Jobs for {company.CompanyIdentifier}");
                            ResumatorWorker.UpdateJobs(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier, company);

                            DDLogger.LogInfo<CVProcessController>($"{model.TransactionIdentifier} - {startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier} - Start Update Candidates for {company.CompanyIdentifier}");
                            ResumatorWorker.UpdateCandidates(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier, company);
                        }
                    }
                    
                    _logger.LogInformation($"Executed CVProcess.StartFormatCVProcesses for: {model.TransactionIdentifier}");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model), true, String.Empty);
            });

            return Ok();
        }

        /// <summary>
        /// Entry point for the Get Differencial CV List
        /// </summary>
        [HttpPost("TestStartResumatorSynchronization")]
        public IActionResult TestStartResumatorSynchronization()
        {
            List<LLMCompanyData> companies = LLMCompanyDataDAL.ListCompanies();
            foreach (var company in companies)
            {
                if (company.CVSynchronizationEnabled)
                {
                    var activityModel = new ActivityModel()
                    {
                        BaseAssetsFilePath = "Test",
                        DatabaseIdentifier = Guid.NewGuid().ToString(),
                        DomainIdentifier = Guid.NewGuid(),
                        TransactionIdentifier = Guid.NewGuid()
                    };

                    DDLogger.LogInfo<CVProcessController>($"{activityModel.TransactionIdentifier} - Start Update Jobs for {company.CompanyIdentifier}");
                    ResumatorWorker.UpdateJobs(activityModel, Guid.NewGuid(), company);
                    DDLogger.LogInfo<CVProcessController>($"{activityModel.TransactionIdentifier} - Start Update Candidates for {company.CompanyIdentifier}");
                    ResumatorWorker.UpdateCandidates(activityModel, Guid.NewGuid(), company);
                }
            }

            return Ok();
        }


        /// <summary>
        /// Entry point for the Download Documents for Application
        /// </summary>
        [HttpPost("DownloadDocuments")]
        public IActionResult DownloadDocuments(StartActivityOut startActivityPayload)
        {
            //Do something async
            Task.Run(() =>
            {
                var model = ProtoBufSerializer.Deserialize<ActivityModel>(startActivityPayload.MarshalledExternalData);

                if (!startActivityPayload.TestMode)
                {
                    _logger.LogInformation($"Executing CVProcess.DownloadDocuments for: {model.TransactionIdentifier}");

                    //Obtain the company data
                    LLMCompanyData company = LLMCompanyDataDAL.Get(model.CompanyIdentifier);

                    //Obtain the LLMProcessData object
                    var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
                    if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.StartPayloadKey))
                    {
                        //No Start Data was uploaded - Do nothing as we cannot proceed
                        DDLogger.LogWarn<FileWorker>($"{model.TransactionIdentifier} - DownloadDocuments - Invoked without StartPayload loaded in Process Data");
                        return;
                    }

                    Dictionary<string, string> startPayload = BsonSerializer.Deserialize<Dictionary<string, string>>(llmProcessData.ProcessData[LLMProcessDataConstants.StartPayloadKey]);
                    if (startPayload == null || !startPayload.Any() || !startPayload.ContainsKey("candidateEmail") || !startPayload.ContainsKey("applicationId"))
                    {
                        //Start Data invalid or inexistent - Do nothing as we cannot proceed
                        DDLogger.LogWarn<FileWorker>($"{model.TransactionIdentifier} - DownloadDocuments - Invoked with a StartPayload invalid or inexistent");
                    }

                    ResumatorWorker.DownloadDocuments(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier, company, startPayload["candidateEmail"], startPayload["applicationId"]);

                    _logger.LogInformation($"Executed CVProcess.StartFormatCVProcesses for: {model.TransactionIdentifier}");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model), true, String.Empty);
            });

            return Ok();
        }

        /// <summary>
        /// Entry point for the Test DownloadDocuments - No longer working as DownloadDocuments now requires a correct database identifier
        /// </summary>
        [HttpPost("TestDownloadDocuments")]
        [Consumes("multipart/form-data")]
        public IActionResult TestDownloadDocuments([FromForm] string companyIdentifier, [FromForm] string candidateEmail, [FromForm] string applicationId)
        {
            //Obtain the company data
            LLMCompanyData company = LLMCompanyDataDAL.Get(companyIdentifier);

            var model = new ActivityModel()
            {
                BaseAssetsFilePath = @"c:\Temp\LLMDriver\Tests",
                DatabaseIdentifier = Guid.NewGuid().ToString(),
                DomainIdentifier = Guid.NewGuid(),
                TransactionIdentifier = Guid.NewGuid()
            };

            ResumatorWorker.DownloadDocuments(model, Guid.NewGuid(), company, candidateEmail, applicationId);

            return Ok();
        }
    }
}
