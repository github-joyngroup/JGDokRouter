using DocDigitizer.Common.Logging;
using NHibernate.Criterion;
using OllamaSharp;
using OllamaSharp.Models;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class OllamaClient
    {
        private const string DefaultOllamaModel = "llama3";
        /*Where we store all the conversations*/
        private static readonly ConcurrentDictionary<string, ConversationContext> UserContexts = new();

        private static OllamaClientSettings Settings;
        private static OllamaApiClient Client;
        /// <summary>
        /// Used only for logging and debugging purposes as Settings.Model can be changed at runtime to the DefaultOllamaModel
        /// </summary>
        private static string InitialModel;
        private static readonly object clientLocker = new();
        public static void Startup(OllamaClientSettings settings)
        {
            if (settings == null) { throw new ArgumentNullException(nameof(settings)); }

            lock (clientLocker)
            {
                Settings = settings;
                Settings.Model = String.IsNullOrWhiteSpace(Settings.Model) ? DefaultOllamaModel : Settings.Model;
                InitialModel = Settings.Model;

                Client = new OllamaApiClient(Settings.Url, Settings.Model);
            }

            InitModelIfNeeded(true);
            DDLogger.LogInfo<OllamaClient>($"OllamaClient started using model '{Settings.Model}' on API url '{Settings.Url}'");
        }

        private static void InitModelIfNeeded(bool bLoadIfNotFound)
        {
            bool bFoundModel = false;
            var modelsTask = Client.ListLocalModels();
            modelsTask.Wait();
            DDLogger.LogDebug<OllamaClient>($"Found {modelsTask.Result.Count()} Local models");
            foreach (var model in modelsTask.Result)
            {
                if(model.Name.Contains(Settings.Model.ToLower().Trim()))
                {
                    DDLogger.LogInfo<OllamaClient>($" - {model.Name} - SELECTED MODEL");
                    bFoundModel = true;
                }
                else
                {
                    DDLogger.LogDebug<OllamaClient>($" - {model.Name}");
                }
            }

            if (!bFoundModel)
            {
                //Model not found. will try to pull it and retry, if already tried, will revert to default model and retry
                //If even so it fails, will throw an exception as OllamaClient cannot be used without a model

                if (!bFoundModel && bLoadIfNotFound)
                {
                    DDLogger.LogInfo<OllamaClient>($"{Settings.Model} - Not loaded - will issue pull command");

                    double latestPercent = 0;
                    string latestStatus = "";
                    var pullModelTask = Client.PullModel(Settings.Model, status =>
                    {
                        if(latestStatus != status.Status)
                        {
                            latestPercent = 0;
                        }
                        if (status.Percent > latestPercent && status.Percent % 10 == 0)
                        {
                            latestStatus = status.Status;
                            latestPercent = status.Percent;
                            DDLogger.LogDebug<OllamaClient>($"Pulling model {Settings.Model} - {status.Percent}% - {status.Status}");
                        }
                    });

                    pullModelTask.Wait();
                    DDLogger.LogDebug<OllamaClient>($"Pulling of model {Settings.Model} completed");

                    //Rerun the init to check if the model is now loaded
                    InitModelIfNeeded(false);
                }
                else if (!bFoundModel && Settings.Model != DefaultOllamaModel)
                {
                    DDLogger.LogError<OllamaClient>($"Model {Settings.Model} not found and load did not add it to local models will retry with DefaultModel {DefaultOllamaModel}");
                    Settings.Model = DefaultOllamaModel;
                    InitModelIfNeeded(true);
                }
                else
                {
                    //Throw the towel
                    var msg = $"Cannot find model {InitialModel} and failed to load it. Also failed reverting to model {DefaultOllamaModel}. OllamaClient cannot be used without a model and cannot continue startup.";
                    DDLogger.LogError<OllamaClient>(msg);
                    throw new Exception(msg);
                }
            }
        }

        /// <summary>
        /// Places a request to the OllamaClient.
        /// If the user context is not found, it will be created, otherwise it will be updated.
        /// If any system messages are configured, they will be included in the context.
        /// </summary>
        /// <param name="userInput">the user prompt</param>
        /// <param name="userId">the user or context identifier</param>
        /// <returns>Returns the assistant response</returns>
        /// <exception cref="Exception">On error calling OllamaClient API</exception>
        public static async Task<string> PlaceRequest(string userInput, string userId)
        {
            if (Client == null) { throw new Exception("OllamaClient not initialized. Startup method should be invoked before first usage."); }
            UserContexts.TryGetValue(userId, out var context);

            try
            {
                DDLogger.LogDebug<OllamaClient>($"OllamaClient userId '{userId}', request:'{userId}'"); 
                var contextWithResponse = await Client.GetCompletion(userInput, context);
                UserContexts[userId] = contextWithResponse;
                
                return contextWithResponse.Response;
            }
            catch (Exception ex)
            {
                DDLogger.LogException<OllamaClient>($"OllamaClient exception making an Ollama request for userId '{userId}'", ex);
                throw;
            }
        }

        public static void ClearContext(string userId)
        {
            UserContexts.TryRemove(userId, out _);
        }
    }

    public class OllamaClientSettings
    {
        public string Url { get; set; }
        public string Model { get; set; }
    }
}
