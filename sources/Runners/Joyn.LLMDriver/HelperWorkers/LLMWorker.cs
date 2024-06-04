using DocDigitizer.Common.DataStructures.Cartesian;
using DocDigitizer.Common.DataStructures.OCR;
using DocDigitizer.Common.Exceptions;
using DocDigitizer.Common.Extensions;
using DocDigitizer.Common.Logging;
using Google.Cloud.Vision.V1;
using Joyn.LLMDriver.DAL;
using Joyn.LLMDriver.Models;
using Joyn.LLMDriver.PSAspects;
using Joyn.Timelog.Common.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using NReco.PdfRenderer;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class LLMWorker
    {
        //Static variables
        private const string ClassifyPromptKey = "classify";
        private const string ClassificationResultKey = "classification";
        
        private const string CheckIfResumePromptKey = "checkifresume";
        private const string IsResumeResultKey = "isResume";
        private const string ResumeClassificationValue = "Resume";

        private const string DocumentContentPlaceholder = "DOCUMENT_CONTENT";
        private static string LLMPromptsLocation;

        private static readonly Dictionary<string, string> LLMPrompts = new Dictionary<string, string>();

        public static void Startup(string llmPromptsLocation)
        {
            LLMPromptsLocation = llmPromptsLocation;
            
            //Pre load all ChatGPT Document classes Prompts
            foreach (var file in Directory.GetFiles(LLMPromptsLocation))
            {
                LLMPrompts[Path.GetFileNameWithoutExtension(file).ToLower().RemoveWhitespaces()] = File.ReadAllText(file);
            }
        }

        #region Classify Using LLM

        //TODO: THIS IS HARDCODED TO USE CHATGPT, SHALL WE CHANGE IT TO BE CONFIGURED. IF SO, WHERE AND HOW?

        /// <summary>
        /// Produces the metadata of the uploaded file within a process
        /// If no file was uploaded, this step is skipped
        /// Metadata produced in this step includes the content type, based on the file extension and the total number of pages in the file
        /// Information is expected to exist within LLMProcessData.ProcessData[LLMProcessDataConstants.FileInformationKey], thats also where the updated information is saved
        /// </summary>
        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._70_ClassifyUsingLLM)]
        public static void ClassifyUsingLLM(ActivityModel model, Guid executionId)
        {
            //Obtain the LLMProcessData object
            var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
            
            //Initial validation
            if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.AssetInformationKey))
            {
                //We don't have nay asset information, cannot classify
                return;
            }

            var consolidatedTextLines = AssetWorker.GetAssetText(model.BaseAssetsFilePath, LLMProcessDataConstants.AssetKeyConsolidatedTextLines, llmProcessData);
            if (String.IsNullOrWhiteSpace(consolidatedTextLines))
            {
                //We don't have any content to classify, cannot continue
                return;
            }

            //Prepare prompt
            var prompt = LLMPrompts[ClassifyPromptKey];
            prompt = prompt.Replace($"{DocumentContentPlaceholder}", consolidatedTextLines);

            Stopwatch sw = new Stopwatch();
            sw.Start();

            var responseTask = GetChatGPTResponseAsync(prompt, $"{model.TransactionIdentifier}");
            responseTask.Wait();
            var response = responseTask.Result??String.Empty;

            sw.Stop();
            DDLogger.LogDebug<LLMWorker>($"LLM Classification Time - Transaction {model.TransactionIdentifier} took {sw.ElapsedMilliseconds}ms");

            //Persist chat gpt asset
            AssetWorker.PutAssetText(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyChatGPTClassify}.json", response, llmProcessData, LLMProcessDataConstants.AssetKeyChatGPTClassify, bUpdateDb: false);

            try
            {
                var llmClassification = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(response);
                if (llmClassification == null)
                {
                    AssetWorker.PutAssetText(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyClassificationError}.txt", "LLM FAIL - null asset deserialization", llmProcessData, LLMProcessDataConstants.AssetKeyClassificationError, bUpdateDb: false);
                }
                else if (!llmClassification.Any())
                {
                    AssetWorker.PutAssetText(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyClassificationError}.txt", "LLM FAIL - empty dictionary deserialization", llmProcessData, LLMProcessDataConstants.AssetKeyClassificationError, bUpdateDb: false);
                }
                else if (!llmClassification.ContainsKey(ClassificationResultKey))
                {
                    AssetWorker.PutAssetText(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyClassificationError}.txt", $"LLM FAIL - '{ClassificationResultKey}' key not found", llmProcessData, LLMProcessDataConstants.AssetKeyClassificationError, bUpdateDb: false);
                }
                else
                {
                    LLMDocumentExtraction llmDocumentExtraction = new LLMDocumentExtraction
                    {
                        Classification = llmClassification[ClassificationResultKey]
                    };
                    llmProcessData.ProcessData[LLMProcessDataConstants.LLMDocumentExtractionKey] = llmDocumentExtraction.ToBsonDocument();
                    DDLogger.LogInfo<LLMWorker>($"LLM Classification Success - Classification: {llmDocumentExtraction.Classification}");
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText($"{model.BaseAssetsFilePath}.error.txt", "LLM FAIL - " + ex.Message);
                AssetWorker.PutAssetText(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyClassificationError}.txt", $"LLM FAIL WITH EXCEPTION\r\n{ex.Message}\r\n------------\r\n{ex.StackTrace}", llmProcessData, LLMProcessDataConstants.AssetKeyClassificationError, bUpdateDb: false);
            }

            LLMProcessDataDAL.SaveOrUpdate(llmProcessData);
        }

        #endregion

        #region Performs an LLM Extraction

        //TODO: THIS IS HARDCODED TO USE CHATGPT, SHALL WE CHANGE IT TO BE CONFIGURED. IF SO, WHERE AND HOW?

        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._70_PerformLLMExtraction)]
        public static void PerformLLMExtraction(ActivityModel model, Guid executionId)
        {
            //Obtain the LLMProcessData object
            var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
            if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.FileInformationKey))
            {
                //No File was uploaded - Do nothing as we cannot proceed without a file
                DDLogger.LogWarn<LLMWorker>($"No File was uploaded for {model.TransactionIdentifier}");
                return;
            }

            //Initial validation
            if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.AssetInformationKey))
            {
                //We don't have nay asset information, cannot classify
                DDLogger.LogWarn<LLMWorker>($"{model.TransactionIdentifier} cannot map to any process data with database identifier: {model.DatabaseIdentifier}");
                return;
            }

            var consolidatedTextLines = AssetWorker.GetAssetText(model.BaseAssetsFilePath, LLMProcessDataConstants.AssetKeyConsolidatedTextLines, llmProcessData);
            if (String.IsNullOrWhiteSpace(consolidatedTextLines))
            {
                //We don't have any content to extract, cannot continue
                DDLogger.LogWarn<LLMWorker>($"No content for extraction in transaction {model.TransactionIdentifier}");
                return;
            }

            var llmDocumentExtraction = llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.LLMDocumentExtractionKey) ? BsonSerializer.Deserialize<LLMDocumentExtraction>(llmProcessData.ProcessData[LLMProcessDataConstants.LLMDocumentExtractionKey]) : null;
            if(llmDocumentExtraction == null || String.IsNullOrWhiteSpace(llmDocumentExtraction.Classification))
            {
                //We don't have any classification, cannot continue
                DDLogger.LogWarn<LLMWorker>($"No classification for extraction in transaction {model.TransactionIdentifier}");
                return;
            }

            var classification = llmDocumentExtraction.Classification.ToLower().RemoveWhitespaces();
            if (!LLMPrompts.ContainsKey(classification))
            {
                //Try hot loading the prompt
                var promptDefinitionFilePath = Path.Combine(LLMPromptsLocation, $"{classification}.txt");
                if (File.Exists(promptDefinitionFilePath))
                {
                    LLMPrompts[classification] = File.ReadAllText(promptDefinitionFilePath);
                }
            }

            if (!LLMPrompts.ContainsKey(classification))
            {
                //We don't have a prompt for this classification, cannot continue
                DDLogger.LogWarn<LLMWorker>($"No LLM prompt for extraction in transaction {model.TransactionIdentifier}");
                return;
            }

            //Prepare prompt
            var prompt = LLMPrompts[classification];
            prompt = prompt.Replace($"{DocumentContentPlaceholder}", consolidatedTextLines);

            Stopwatch sw = new Stopwatch();
            sw.Start();

            var responseTask = GetChatGPTResponseAsync(prompt, $"{model.TransactionIdentifier}");
            responseTask.Wait();
            var response = responseTask.Result;

            sw.Stop();
            DDLogger.LogDebug<LLMWorker>($"LLM Extraction Time - Transaction {model.TransactionIdentifier} took {sw.ElapsedMilliseconds}ms");

            //Persist chat gpt asset
            if(!String.IsNullOrWhiteSpace(response))
            {
                AssetWorker.PutAssetText(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyChatGPTExtract}.json", response, llmProcessData, LLMProcessDataConstants.AssetKeyChatGPTExtract, bUpdateDb: true);
            }
            else
            {
                DDLogger.LogWarn<LLMWorker>($"No LLM extraction produced in transaction {model.TransactionIdentifier}");
            }
        }

        #endregion


        #region Check if document is a Resume (CV)

        //TODO: THIS IS HARDCODED TO USE OLLAMA, SHALL WE CHANGE IT TO BE CONFIGURED. IF SO, WHERE AND HOW?
        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._70_CheckIfResume)]
        public static void CheckIfResume(ActivityModel model, Guid executionId)
        {
            //Obtain the LLMProcessData object
            var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
            if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.FileInformationKey))
            {
                //No File was uploaded - Do nothing as we cannot proceed without a file
                DDLogger.LogWarn<LLMWorker>($"No File was uploaded for {model.TransactionIdentifier}");
                return;
            }

            //Initial validation
            if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.AssetInformationKey))
            {
                //We don't have nay asset information, cannot classify
                DDLogger.LogWarn<LLMWorker>($"{model.TransactionIdentifier} cannot map to any process data with database identifier: {model.DatabaseIdentifier}");
                return;
            }

            var consolidatedTextLines = AssetWorker.GetAssetText(model.BaseAssetsFilePath, LLMProcessDataConstants.AssetKeyConsolidatedTextLines, llmProcessData);
            if (String.IsNullOrWhiteSpace(consolidatedTextLines))
            {
                //We don't have any content to extract, cannot continue
                DDLogger.LogWarn<LLMWorker>($"No content for extraction in transaction {model.TransactionIdentifier}");
                return;
            }

            //Prepare prompt
            var prompt = LLMPrompts[CheckIfResumePromptKey];
            prompt = prompt.Replace($"{DocumentContentPlaceholder}", consolidatedTextLines);

            Stopwatch sw = new Stopwatch();
            sw.Start();

            var responseTask = GetOllamaResponseAsync(prompt, $"{model.TransactionIdentifier}");
            responseTask.Wait();
            var response = responseTask.Result ?? String.Empty;

            sw.Stop();
            DDLogger.LogDebug<LLMWorker>($"LLM Check if Resume Time - Transaction {model.TransactionIdentifier} took {sw.ElapsedMilliseconds}ms");

            //Persist chat gpt asset
            AssetWorker.PutAssetText(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyOllamaResumeCheck}.json", response, llmProcessData, LLMProcessDataConstants.AssetKeyOllamaResumeCheck, bUpdateDb: false);

            try
            {
                var isResume = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(response);
                if (isResume == null)
                {
                    AssetWorker.PutAssetText(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyIsResumeError}.txt", "LLM FAIL - null asset deserialization", llmProcessData, LLMProcessDataConstants.AssetKeyIsResumeError, bUpdateDb: false);
                }
                else if (!isResume.Any())
                {
                    AssetWorker.PutAssetText(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyIsResumeError}.txt", "LLM FAIL - empty dictionary deserialization", llmProcessData, LLMProcessDataConstants.AssetKeyIsResumeError, bUpdateDb: false);
                }
                else if (!isResume.ContainsKey(IsResumeResultKey))
                {
                    AssetWorker.PutAssetText(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyIsResumeError}.txt", $"LLM FAIL - '{ClassificationResultKey}' key not found", llmProcessData, LLMProcessDataConstants.AssetKeyIsResumeError, bUpdateDb: false);
                }
                else
                {
                    var isResumeResult = bool.Parse(isResume[IsResumeResultKey]);
                    //llmProcessData.ProcessData[LLMProcessDataConstants.LLMDocumentExtractionKey] = llmDocumentExtraction.ToBsonDocument();
                    if (isResumeResult)
                    {
                        //Add Resume classification to the process data so it can be used for further processing
                        LLMDocumentExtraction llmDocumentExtraction = new LLMDocumentExtraction
                        {
                            Classification = ResumeClassificationValue
                        };
                    }
                    DDLogger.LogInfo<LLMWorker>($"LLM IsResume Success - IsResume: {isResumeResult}");
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText($"{model.BaseAssetsFilePath}.error.txt", "LLM FAIL - " + ex.Message);
                AssetWorker.PutAssetText(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyIsResumeError}.txt", $"LLM FAIL WITH EXCEPTION\r\n{ex.Message}\r\n------------\r\n{ex.StackTrace}", llmProcessData, LLMProcessDataConstants.AssetKeyIsResumeError, bUpdateDb: false);
            }

            LLMProcessDataDAL.SaveOrUpdate(llmProcessData);
        }

        #endregion

        #region Generic Chat GPT Access Method

        private static async Task<string> GetChatGPTResponseAsync(string prompt, string identifier)
        {
            try
            {
                var chatGPTResult = await ChatGPTClient.PlaceRequest(prompt, identifier);
                return chatGPTResult;
            }
            catch (Exception ex) //We don't want to keep the asset factory retrying and consuming ChatGPT tokens, so we catch the exception and return and empty response
            {
                DDLogger.LogException<LLMWorker>($"ChatGPTClient requestID '{identifier}'", ex);
                return null;
            }
        }

        #endregion

        #region Generic Ollama Access Method

        private static async Task<string> GetOllamaResponseAsync(string prompt, string identifier)
        {
            try
            {
                var ollamaResult = await OllamaClient.PlaceRequest(prompt, identifier);
                return ollamaResult;
            }
            catch (Exception ex) //We don't want to keep the asset factory retrying and consuming Ollama tokens, so we catch the exception and return and empty response
            {
                DDLogger.LogException<LLMWorker>($"OLlamaClient requestID '{identifier}'", ex);
                return null;
            }
        }

        #endregion
    }
}
