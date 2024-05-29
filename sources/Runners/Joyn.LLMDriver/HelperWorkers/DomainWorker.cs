using DocDigitizer.Common.Logging;
using Joyn.DokRouter.Common;
using Joyn.LLMDriver.Controllers;
using Joyn.LLMDriver.DAL;
using Joyn.LLMDriver.Models;
using MongoDB.Bson;
using static Google.Rpc.Context.AttributeContext.Types;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class DomainWorker
    {
        private static DomainWorkerConfiguration _configuration;

        public static void Startup(DomainWorkerConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public static ActivityModel StartPipelineByDomain(Guid? transactionIdentifier, Guid domainIdentifier, Guid? pipelineToStartIdentifier, string companyIdentifier, Dictionary<string, string> startPayload, IFormFile uploadedFile)
        {
            //TODO: Check if domain exists and load specific domain configurations - pipelineToStartIdentifier should come from domain configuration

            //Generate transaction identifier
            if (!transactionIdentifier.HasValue) { transactionIdentifier = Guid.NewGuid(); }

            //Create working directory
            var processFolder = Path.Combine(_configuration.BaseWorkingFolderPath, domainIdentifier.ToString(), "processes", transactionIdentifier.Value.ToString());

            DDLogger.LogDebug<DomainController>($"Creating Process folder: '{processFolder}'");
            Directory.CreateDirectory(processFolder);

            string baseAssetsFilePath = Path.Combine(processFolder, "assets");
            Directory.CreateDirectory(baseAssetsFilePath);

            ActivityModel activityModel = new ActivityModel()
            {
                TransactionIdentifier = transactionIdentifier.Value,
                DatabaseIdentifier = transactionIdentifier.Value.ToString(),
                DomainIdentifier = domainIdentifier,
                BaseAssetsFilePath = baseAssetsFilePath,
                CompanyIdentifier = companyIdentifier
            };

            LLMProcessData llmProcessData = new LLMProcessData()
            {
                Id = transactionIdentifier.Value.ToString(),
                CreatedAt = DateTime.UtcNow,

                ProcessData = new Dictionary<string, BsonDocument>()
                {
                    { LLMProcessDataConstants.ActivityModelKey, activityModel.ToBsonDocument() }
                }
            };

            if(startPayload != null)
            {
                llmProcessData.ProcessData[LLMProcessDataConstants.StartPayloadKey] = startPayload.ToBsonDocument();
            }

            LLMProcessDataDAL.SaveOrUpdate(llmProcessData);

            //If a file is received, save it to the assets folder
            if (uploadedFile != null)
            {
                var filePath = Path.Combine(activityModel.BaseAssetsFilePath, "original.pdf");

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
            if (pipelineToStartIdentifier.HasValue)
            {
                Common.StartPipeline(pipelineToStartIdentifier.Value, transactionIdentifier.Value, ProtoBufSerializer.Serialize(activityModel), _configuration.DokRouterStartPipelineUrl);
            }

            return activityModel;
        }
    }

    public class DomainWorkerConfiguration
    {
        public string BaseWorkingFolderPath { get; set; }
        public string DokRouterStartPipelineUrl { get; set; }
    }
}
