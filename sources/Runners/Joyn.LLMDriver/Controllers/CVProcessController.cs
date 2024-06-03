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
                            ResumatorWorker.UpdateApplications(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier, company);
                        }
                    }
                    
                    _logger.LogInformation($"Executed CVProcess.StartFormatCVProcesses for: {model.TransactionIdentifier}");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model));
            });

            return Ok();
        }

        /// <summary>
        /// Entry point for the Download Documents for Application
        /// </summary>
        [HttpPost("UpdateCandidate")]
        public IActionResult UpdateCandidate(StartActivityOut startActivityPayload)
        {
            //Do something async
            Task.Run(() =>
            {
                var model = ProtoBufSerializer.Deserialize<ActivityModel>(startActivityPayload.MarshalledExternalData);

                if (!startActivityPayload.TestMode)
                {
                    _logger.LogInformation($"Executing CVProcess.UpdateCandidate for: {model.TransactionIdentifier}");

                    //Obtain the company data
                    LLMCompanyData companyData = LLMCompanyDataDAL.Get(model.CompanyIdentifier);

                    //Obtain the LLMProcessData object
                    var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
                    if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.StartPayloadKey))
                    {
                        //No Start Data was uploaded - Do nothing as we cannot proceed
                        DDLogger.LogWarn<FileWorker>($"{model.TransactionIdentifier} - UpdateCandidate - Invoked without StartPayload loaded in Process Data");
                        return;
                    }

                    Dictionary<string, string> startPayload = BsonSerializer.Deserialize<Dictionary<string, string>>(llmProcessData.ProcessData[LLMProcessDataConstants.StartPayloadKey]);
                    if (startPayload == null || !startPayload.Any() || !startPayload.ContainsKey("applicationId") )
                    {
                        //Start Data invalid or inexistent - Do nothing as we cannot proceed
                        DDLogger.LogWarn<FileWorker>($"{model.TransactionIdentifier} - UpdateCandidate - Invoked with a StartPayload invalid or inexistent");
                    }

                    ResumatorWorker.UpdateCandidate(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier, companyData, startPayload["applicationId"]);

                    _logger.LogInformation($"Executed CVProcess.UpdateCandidate for: {model.TransactionIdentifier}");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model));
            });

            return Ok();
        }

        /// <summary>
        /// Entry point for the Update Documents
        /// </summary>
        [HttpPost("UpdateDocuments")]
        public IActionResult UpdateDocuments(StartActivityOut startActivityPayload)
        {
            //Do something async
            Task.Run(() =>
            {
                var model = startActivityPayload.MarshalledExternalData != null ? ProtoBufSerializer.Deserialize<ActivityModel>(startActivityPayload.MarshalledExternalData) : null;
                int nDocumentsFound = 0;

                if (!startActivityPayload.TestMode)
                {
                    _logger.LogInformation($"Executing CVProcess.UpdateDocuments for: {model.TransactionIdentifier}");
                    
                    //Obtain the company data
                    LLMCompanyData companyData = LLMCompanyDataDAL.Get(model.CompanyIdentifier);

                    nDocumentsFound = ResumatorWorker.UpdateDocuments(model, startActivityPayload.ActivityExecutionKey.ActivityExecutionIdentifier, companyData);

                    _logger.LogInformation($"Executed CVProcess.UpdateDocuments for: {model.TransactionIdentifier} - Found {nDocumentsFound}");
                }

                Common.CallbackEndActivity(startActivityPayload, ProtoBufSerializer.Serialize(model), new Dictionary<string, string>() { { "numberDocuments", nDocumentsFound.ToString() } });
            });

            return Ok();
        }
    }
}
