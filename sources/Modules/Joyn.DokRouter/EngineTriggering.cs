using DocDigitizer.Common.Logging;
using Joyn.DokRouter.Common;
using Joyn.DokRouter.Common.DAL;
using Joyn.DokRouter.Common.Models;
using Joyn.DokRouter.Common.Payloads;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter
{
    /// <summary>
    /// Will have a background service that will monitor the registered triggers to start automated pipelines
    /// </summary>
    public class EngineTriggering : BackgroundService
    {
        /// <summary>
        /// The configuration for the EngineMonitor
        /// </summary>
        private static EngineTriggeringConfiguration _configuration;

        /// <summary>
        /// Implementation of the Persistence Store
        /// </summary>
        private static IDokRouterDAL _dokRouterDAL;

        /// <summary>
        /// Logger for the EngineTriggering
        /// </summary>
        private static ILogger _logger;

        /// <summary>The cancellation token source to stop the engine trigger - will be flagged on the stop method</summary>
        private static CancellationTokenSource StoppingCancelationTokenSource;

        /// <summary>
        /// Used to pulse and wake the pooling thread when new triggers are registered
        /// </summary>
        readonly static object myWorkerLocker = new object();

        /// <summary>
        /// Keeps track of the registered pipeline triggers, indexed by their identifier
        /// </summary>
        private static ConcurrentDictionary<Guid, PipelineTriggerInstance> pipelineTriggerInstances = new ConcurrentDictionary<Guid, PipelineTriggerInstance>();

        /// <summary>
        /// Setups the EngineTriggering based on the configuration.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        public static void Startup(EngineTriggeringConfiguration configuration, IDokRouterDAL dokRouterDAL, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
            _dokRouterDAL = dokRouterDAL;

            _logger?.LogInformation($"Joyn.DokRouter.EngineTriggering setup.");
        }

        /// <summary>
        /// Starts the EngineTriggering
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation($"Joyn.DokRouter.EngineTriggering is starting...");

            StoppingCancelationTokenSource = new CancellationTokenSource();

            //Start Monitoring
            new Thread(() => MainEngineTriggering(StoppingCancelationTokenSource.Token)).Start();

            return Task.CompletedTask;

        }

        /// <summary>
        /// Stops the EngineTriggering
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"Joyn.DokRouter.EngineTriggering is stopping...");

            StoppingCancelationTokenSource.Cancel();
            //Obtain lock so thread is in synchronized state
            lock (myWorkerLocker)
            {
                //Wake the pooling thread
                Monitor.Pulse(myWorkerLocker);
            }
            await base.StopAsync(StoppingCancelationTokenSource.Token);
        }

        /// <summary>
        /// Main Engine Triggering thread - will be sleeping most of the timerun based on the configured interval
        /// On each run, will obtain the state of the Running Instances from the Persistence Store
        /// This state will be made available for pull requests
        /// If configured push requests hooks, they will be triggered. Note that they might be configured to only be triggered if some activity expired it's pipeline
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the reception of data</param>
        public static void MainEngineTriggering(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Joyn.DokRouter.EngineTriggering is now running.");

            while (!cancellationToken.IsCancellationRequested)
            {
                lock (myWorkerLocker)
                {
                    try
                    {
                        var utcNow = DateTime.UtcNow;
                        List<Task> triggeringTasks = new List<Task>();
                        //Check if any registered trigger has expired, if so, trigger it's execute method
                        foreach (var pipelineTrigger in pipelineTriggerInstances.Values)
                        {
                            if (!pipelineTrigger.NextExecution.HasValue || pipelineTrigger.NextExecution < utcNow)
                            {
                                triggeringTasks.Add(Task.Run(() => TriggerPipelineTrigger(pipelineTrigger)));
                            }
                        }

                        Task.WaitAll(triggeringTasks.ToArray());

                        //Sleep until pulsed or until the next expected trigger moment, capped at configured min frequency
                        int secondsToSleep = _configuration.EngineTriggeringMinFrequencyInSeconds;
                        foreach (var pipelineTrigger in pipelineTriggerInstances.Values)
                        {
                            var pipelineTriggerSeconds = (int)((pipelineTrigger.NextExecution ?? utcNow) - utcNow).TotalSeconds;
                            secondsToSleep = Math.Min(secondsToSleep, pipelineTriggerSeconds);
                        }

                        Monitor.Wait(myWorkerLocker, secondsToSleep * 1000);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Joyn.DokRouter.EngineTriggering error occurred: {ex.Message}", ex);
                    }
                }
            }

            _logger.LogInformation($"Joyn.DokRouter.EngineTriggering stopped.");
        }

        /// <summary>
        /// Helper method to wake the pooling thread
        /// </summary>
        private static void Pulse()
        {
            //Obtain lock so thread is in synchronized state
            lock (myWorkerLocker)
            {
                //Wake the pooling thread
                Monitor.Pulse(myWorkerLocker);
            }
        }

        /// <summary>
        /// Adds a pipeline trigger to the collection of triggers
        /// </summary>
        public static void RegisterPipelineTrigger(PipelineTriggerInstance pipelineTrigger)
        {
            pipelineTriggerInstances.TryAdd(pipelineTrigger.Identifier, pipelineTrigger);
            DDLogger.LogInfo<EngineTriggering>($"Registered Pipeline Trigger {pipelineTrigger.Identifier} for pipeline {pipelineTrigger.PipelineIdentifier}");
            Pulse();
        }

        /// <summary>
        /// Removes a pipeline trigger from the collection of triggers
        /// </summary>
        public static void UnregisterPipelineTrigger(Guid pipelineTriggerIdentifier)
        {
            pipelineTriggerInstances.TryRemove(pipelineTriggerIdentifier, out var pipelineTriggerInstance);
            if (pipelineTriggerInstance != null)
            {
                DDLogger.LogInfo<EngineTriggering>($"Unregistered Pipeline Trigger {pipelineTriggerInstance.Identifier} for pipeline {pipelineTriggerInstance.PipelineIdentifier}");
            }
        }

        /// <summary>
        /// Triggers the pipeline trigger making it execute.
        /// If a pre condition exist, ti will be started and the pipeline will only be triggered upon the return of that execution
        /// Otherwise it will immediatly trigger the pipeline
        /// </summary>
        /// <param name="pipelineTrigger"></param>
        private static void TriggerPipelineTrigger(PipelineTriggerInstance pipelineTriggerInstance)
        {
            if(pipelineTriggerInstance.PreConditionActivity != null)
            {
                //Launch Pre Condition Activity
                ActivityStarter.OnStartActivity(pipelineTriggerInstance.PreConditionActivity, new StartActivityOut()
                {
                    ActivityExecutionKey = new ActivityExecutionKey()
                    {
                        ActivityDefinitionIdentifier = pipelineTriggerInstance.PreConditionActivity.Configuration.Identifier,
                        ActivityExecutionIdentifier = Guid.NewGuid(),
                        PipelineInstanceKey = null, //Not within pipeline
                        PipelineTriggerIdentifier = pipelineTriggerInstance.Identifier
                    }
                });
            }
            else
            {
                StartPipeline(pipelineTriggerInstance);
            }
        }

        /// <summary>
        /// Will be called by the finish activity and will check if when the Pre Condition Activity ends
        /// </summary>
        /// <param name="startActivityPayload"></param>
        public static void OnPreConditionActivityEnd(EndActivity endActivityPayload)
        {
            PipelineTriggerInstance pipelineTriggerInstance;
            pipelineTriggerInstances.TryGetValue(endActivityPayload.ActivityExecutionKey.PipelineTriggerIdentifier.Value, out pipelineTriggerInstance);
            if(pipelineTriggerInstance == null)
            {
                DDLogger.LogWarn<EngineTriggering>($"OnPreConditionActivityEnd - PipelineTrigger not found for ActivityExecutionKey: {endActivityPayload.ActivityExecutionKey}");
                return;
            }

            String preConditionReturnValueStr = String.Empty;
            endActivityPayload.ProcessInstanceData.TryGetValue(pipelineTriggerInstance.ExpectedPreConditionField, out preConditionReturnValueStr);
            bool preConditionReturnValue = false;
            bool.TryParse(preConditionReturnValueStr, out preConditionReturnValue);
            if (preConditionReturnValue)
            {
                StartPipeline(pipelineTriggerInstance);
            }
            else
            {
                DDLogger.LogDebug<EngineTriggering>($"Pre condition was false for pipeline trigger {pipelineTriggerInstance.Identifier}, not triggering pipeline.");

                //update the next execution moment
                pipelineTriggerInstance.LastExecution = DateTime.UtcNow;
                pipelineTriggerInstance.NextExecution = CalculateNextExecution(pipelineTriggerInstance);
            }
        }   

        /// <summary>
        /// Sends the StartPipeline command to the Main Engine
        /// It will also update the LastExecution and NextExecution moments
        /// </summary>
        private static void StartPipeline(PipelineTriggerInstance pipelineTriggerInstance)
        {
            DDLogger.LogInfo<EngineTriggering>($"Trigger {pipelineTriggerInstance.Identifier} will start it's pipeline {pipelineTriggerInstance.PipelineIdentifier}");

            MainEngine.StartPipeline(new StartPipeline()
            {
                MarshalledExternalData = null, //If we want some context, it will be placed here, but for now, we don't need it
                PipelineDefinitionIdentifier = pipelineTriggerInstance.PipelineIdentifier,
                TransactionIdentifier = Guid.NewGuid(),
            });

            //update the next execution moment
            pipelineTriggerInstance.LastExecution = DateTime.UtcNow;
            pipelineTriggerInstance.NextExecution = CalculateNextExecution(pipelineTriggerInstance);
        }

        /// <summary>
        /// Based on the kind of pipeline trigger it will calculate the next execution moment
        /// </summary>
        private static DateTime CalculateNextExecution(PipelineTriggerInstance pipelineTriggerInstance)
        {
            switch (pipelineTriggerInstance.Kind)
            {
                case PipelineTriggerKind.TimerFrequency:
                    return (pipelineTriggerInstance.LastExecution??DateTime.UtcNow).AddSeconds(pipelineTriggerInstance.TimeFrequencySeconds.Value);

                default:
                    throw new NotImplementedException($"PipelineTriggerKind {pipelineTriggerInstance.Kind} not implemented for EngineTriggering");
            }
        }
    }

    /// <summary>
    /// Defines the configurations to be applied on the EngineMonitor
    /// </summary>
    public class EngineTriggeringConfiguration
    {
        /// <summary>
        /// Frequency, in seconds, that the Main EngineTriggering will tick
        /// </summary>
        public int EngineTriggeringMinFrequencyInSeconds { get; set; }

    }
}
