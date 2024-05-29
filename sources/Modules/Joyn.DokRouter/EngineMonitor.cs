using Joyn.DokRouter.Common;
using Joyn.DokRouter.Common.DAL;
using Joyn.DokRouter.Common.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter
{
    public class EngineMonitor : BackgroundService
    {
        /// <summary>
        /// The configuration for the EngineMonitor
        /// </summary>
        private static EngineMonitorConfiguration _configuration;

        /// <summary>
        /// Implementation of the Persistence Store
        /// </summary>
        private static IDokRouterDAL _dokRouterDAL;

        /// <summary>
        /// Logger for the EngineMonitor
        /// </summary>
        private static ILogger _logger;

        /// <summary>The cancellation token source to stop the monitor - will be flagged on the stop method</summary>
        private static CancellationTokenSource StoppingCancelationTokenSource;

        /// <summary>
        /// Starts the EngineMonitor based on the configuration.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        public static void Startup(EngineMonitorConfiguration configuration, IDokRouterDAL dokRouterDAL, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
            _dokRouterDAL = dokRouterDAL;

            _logger?.LogInformation($"Joyn.DokRouter.EngineMonitor setup.");
        }

        /// <summary>
        /// Starts the ViewerServer
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation($"Joyn.DokRouter.EngineMonitor is starting...");

            StoppingCancelationTokenSource = new CancellationTokenSource();

            //Start Monitoring
            new Thread(() => MainMonitor(StoppingCancelationTokenSource.Token)).Start();

            return Task.CompletedTask;

        }

        /// <summary>
        /// Stops the ViewerServer
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"Joyn.DokRouter.EngineMonitor is stopping...");

            StoppingCancelationTokenSource.Cancel();

            await base.StopAsync(StoppingCancelationTokenSource.Token);
        }

        /// <summary>
        /// Main Monitor thread - will run based on the configured interval
        /// On each run, will obtain the state of the Running Instances from the Persistence Store
        /// This state will be made available for pull requests
        /// If configured push requests hooks, they will be triggered. Note that they might be configured to only be triggered if some activity expired it's pipeline
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the reception of data</param>
        public static void MainMonitor(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Joyn.DokRouter.EngineMonitor is now running. Will tick each: {_configuration.MainMonitorFrequencyInSeconds} seconds.");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    //Obtain the state of the Running Instances from the Persistence Store
                    //As, by design, we cannot obtain all data on a single call, we will iterate through the pages until we get all data
                    //However, this may cause a problem if we have many running instances, as we will be loading all of them in memory
                    //Should a limit be imposed? And we would only load up to a limit? If so, we also need to change the finished pipeline method to allow loading remaining pipelines up to that same limit

                    //Load Running instances from DB 
                    var runningInstances = _dokRouterDAL.GetRunningInstances();

                    //Fill expired fields
                    foreach (var pipelineInstance in runningInstances)
                    {
                        FillExpired(pipelineInstance, OnExpiredActivityTry);
                    }

                    Thread.Sleep(_configuration.MainMonitorFrequencyInSeconds * 1000);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Timelog.Server error occurred: {ex.Message}", ex);
                }
            }

            _logger.LogInformation($"Joyn.DokRouter.EngineMonitor stopped.");
        }

        private static void FillExpired(PipelineInstance pipelineInstance, Action<PipelineInstance, ActivityInstance, ActivityExecution> onExpiredActivityTry)
        {
            if (pipelineInstance.PipelineSLAMoment < DateTime.UtcNow)
            {
                pipelineInstance.PipelineSLAExpiredBy = DateTime.UtcNow - pipelineInstance.PipelineSLAMoment;
            }

            if (!pipelineInstance.InstructionInstances.Any())
            {
                //Return - Pipeline has no activities yet
                return;
            }

            //As we sequentially execute the instructions, we can just check the last instructioninstance, as it will be the one that is currently being executed
            var lastInstructionInstance = pipelineInstance.InstructionInstances[pipelineInstance.InstructionInstances.Keys.Max()];

            if (lastInstructionInstance.ActivityInstances == null || !lastInstructionInstance.ActivityInstances.Any())
            {
                //Return - Pipeline Instruction has no activities yet
                return;
            }

            //As we sequentially execute the cycles, we can just check the last activityinstance cycle, as it will be the one that is currently being executed
            var lastActivityInstanceCycle = lastInstructionInstance.ActivityInstances[lastInstructionInstance.ActivityInstances.Keys.Max()];
            if(lastActivityInstanceCycle == null || !lastActivityInstanceCycle.Any())
            {
                //Return - Pipeline Instruction Activity Cycle has no activities yet
                return;
            }

            //Check if any activity expired, if so trigger the onExpiredActivityTry
            foreach(var activityInstance in lastActivityInstanceCycle.Values)
            {
                if (activityInstance.ActivitySLAMoment < DateTime.UtcNow)
                {
                    activityInstance.ActivitySLAExpiredBy = DateTime.UtcNow - activityInstance.ActivitySLAMoment;
                }

                if (activityInstance.RetryOnSLAExpired)
                {
                    foreach (var activityExecution in activityInstance.Executions.Where(e => !e.EndedAt.HasValue))
                    {
                        if (activityExecution.ActivityTrySLAMoment < DateTime.UtcNow)
                        {
                            activityExecution.ActivityTrySLAExpiredBy = DateTime.UtcNow - activityExecution.ActivityTrySLAMoment;
                            onExpiredActivityTry(pipelineInstance, activityInstance, activityExecution);
                        }
                    }
                }
            }
        }

        private static void OnExpiredActivityTry(PipelineInstance pipelineInstance, ActivityInstance activityInstance, ActivityExecution activityExecution)
        {
            _logger.LogWarning($"Asking Engine to (re)Start an activity, as the current execution expired: PipelineInstance: {pipelineInstance.Name} ({pipelineInstance.Key.PipelineDefinitionIdentifier}) - Activity: {activityInstance.Name}) - Execution #{activityInstance.Executions.Count}.");
            //Ask the engine to (re)Start the activity, it will flag the current execution as ended and create a new one
            MainEngine.StartActivity(new Common.Payloads.StartActivityIn()
            {
                PipelineInstanceKey = pipelineInstance.Key
            });
        }
    }

    /// <summary>
    /// Defines the configurations to be applied on the EngineMonitor
    /// </summary>
    public class EngineMonitorConfiguration
    {
        /// <summary>
        /// Frequency, in seconds, that the Main Monitor will tick
        /// </summary>
        public int MainMonitorFrequencyInSeconds { get; set; }

    }
}
