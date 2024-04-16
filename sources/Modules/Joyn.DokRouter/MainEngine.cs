using DocDigitizer.Common.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using Joyn.DokRouter.Common;
using Joyn.DokRouter.Common.Payloads;
using Joyn.DokRouter.Common.Models;
using Joyn.DokRouter.Common.DAL;
using System.Linq;
using System.Xml.Linq;
using System;

namespace Joyn.DokRouter
{
    /// <summary>
    /// The main engine of the DokRouter, responsible for starting and managing pipelines and their activities
    /// </summary>
    public class MainEngine
    {
        private static readonly object dokRouterEngineConfigurationLocker = new object();

        /// <summary>
        /// The hash for the most recent configuration of the DokRouter Engine
        /// </summary>
        private static string LatestConfigurationHash { get; set; }
        
        /// <summary>
        /// The default pipeline identifier to be used when no pipeline is specified
        /// </summary>
        private static Guid? DefaultPipelineIdentifier { get; set; }

        /// <summary>
        /// Dictionary of pipeline definitions, indexed by their identifier, and indexed by the hash of their configuration
        /// </summary>
        private static Dictionary<string, Dictionary<Guid, PipelineDefinition>> PipelineDefinitions { get; set; } = new Dictionary<string, Dictionary<Guid, PipelineDefinition>>();

        /// <summary>
        /// Dictionary of start activity handlers, indexed by the hash of their configuration
        /// </summary>
        private static Dictionary<string, OnStartActivityHandler> OnStartActivityHandlers = new Dictionary<string, OnStartActivityHandler>();

        /// <summary>
        /// Persistence Layer Implementation
        /// </summary>
        private static IDokRouterDAL DokRouterDAL;

        /// <summary>
        /// Current running instances of the engine, indexed by their key
        /// </summary>
        private static ConcurrentDictionary<PipelineInstanceKey, PipelineInstance> RunningInstances = new ConcurrentDictionary<PipelineInstanceKey, PipelineInstance>();

        /// <summary>
        /// Start up method for the engine, will load the configuration and start the engine, must be invoked prior to any other method
        /// </summary>
        public static void Startup(IDokRouterDAL dokRouterDAL)
        {
            lock (dokRouterEngineConfigurationLocker)
            {
                Dictionary<string, DokRouterEngineConfiguration> requiredConfigurations = new Dictionary<string, DokRouterEngineConfiguration>();
                DokRouterDAL = dokRouterDAL;

                //Load latest configuration, if it does not exist in the entire list, it is a new one, so, add it
                var latestEngineConfiguration = DokRouterDAL.GetLatestEngineConfiguration();
                if(latestEngineConfiguration == null) { throw new Exception("Unable to load Latest Configuration from the DAL Implementation"); }

                FillConfigurationHash(latestEngineConfiguration);
                requiredConfigurations.Add(latestEngineConfiguration.Hash, latestEngineConfiguration);

                var existingLatestConfiguration = DokRouterDAL.GetEngineConfigurationByHash(latestEngineConfiguration.Hash);
                if(existingLatestConfiguration == null)
                {
                    DDLogger.LogInfo<MainEngine>($"Detected new configuration with Hash: {latestEngineConfiguration.Hash}, persisting so it can be reused if needed");
                    DokRouterDAL.SaveOrUpdateEngineConfiguration(latestEngineConfiguration);
                }

                LatestConfigurationHash = latestEngineConfiguration.Hash;
                DDLogger.LogInfo<MainEngine>($"Loaded latest configuration with Hash: {LatestConfigurationHash}");

                //Load Running instances from DB 
                //As, by design, we cannot obtain all data on a single call, we will iterate through the pages until we get all data
                //However, this may cause a problem if we have many running instances, as we will be loading all of them in memory
                //Should a limit be imposed? And we would only load up to a limit? If so, we also need to change the finished pipeline method to allow loading remaining pipelines up to that same limit

                var firstPageResult = DokRouterDAL.GetRunningInstances(1);

                var allPagesTasks = Enumerable.Range(2, firstPageResult.lastPage).Select(pageNumber =>
                {
                    return Task.Run(() =>
                    {
                        return DokRouterDAL.GetRunningInstances(pageNumber);
                    });
                }).ToArray();

                Task.WaitAll(allPagesTasks);
                List<PipelineInstance> baseRunningInstances = new List<PipelineInstance>();
                baseRunningInstances.AddRange(firstPageResult.result);
                baseRunningInstances.AddRange(allPagesTasks.SelectMany(t => t.Result.result));

                if (baseRunningInstances.Any())
                {
                    DDLogger.LogInfo<MainEngine>($"Found {baseRunningInstances.Count} pipeline instances running, loading required configurations...");
                }

                RunningInstances = new ConcurrentDictionary<PipelineInstanceKey, PipelineInstance>(baseRunningInstances.ToDictionary(rI => rI.Key, rI => rI));

                List<PipelineInstanceKey> erroredPipelines = new List<PipelineInstanceKey>();                
                //Check if there are any running instances in previous configuration versions, if so, those configurations must be loaded
                foreach(var runningInstanceKey in RunningInstances.Keys)
                {
                    if (!requiredConfigurations.ContainsKey(runningInstanceKey.ConfigurationHash))
                    {
                        var configuration = DokRouterDAL.GetEngineConfigurationByHash(runningInstanceKey.ConfigurationHash);
                        if (configuration == null)
                        {
                            DDLogger.LogError<MainEngine>($"Pipeline Instance '{runningInstanceKey.PipelineInstanceIdentifier}' has a configuration with hash {runningInstanceKey.ConfigurationHash} that was not found - pipeline instance will be errored");
                            erroredPipelines.Add(runningInstanceKey);
                            continue;
                        }
                        
                        requiredConfigurations[runningInstanceKey.ConfigurationHash] = configuration;
                    }
                }

                foreach(var erroredPipeline in erroredPipelines) 
                {
                    ErrorPipeline(RunningInstances[erroredPipeline], "Associated configuration not found");
                }

                foreach (var configuration in requiredConfigurations.Values)
                {
                    PipelineDefinitions[configuration.Hash] = new Dictionary<Guid, PipelineDefinition>();

                    var onExecuteAssembly = Assembly.Load(configuration.OnStartActivityAssembly);
                    if (onExecuteAssembly == null) { throw new Exception($"OnStartActivityAssembly assembly '{configuration.OnStartActivityAssembly}' not found"); }
                    var OnExecuteClass = onExecuteAssembly.GetType(configuration.OnStartActivityClass);
                    if (OnExecuteClass == null) { throw new Exception($"OnStartActivityClass type '{configuration.OnStartActivityClass}' in assembly '{configuration.OnStartActivityAssembly}' not found"); }
                    var onMessageMethod = OnExecuteClass.GetMethod(configuration.OnStartActivityMethod);
                    if (onMessageMethod == null) { throw new Exception($"OnStartActivityMethod method '{configuration.OnStartActivityMethod}' in class '{configuration.OnStartActivityClass}' not found"); }

                    try
                    {
                        OnStartActivityHandlers[configuration.Hash] = (OnStartActivityHandler)Delegate.CreateDelegate(typeof(OnStartActivityHandler), onMessageMethod);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Unable to load Main OnStartActivityHandler configured with method '{configuration.OnStartActivityMethod}' in class '{configuration.OnStartActivityClass}'", ex);
                    }

                    foreach (var pipelineConfiguration in configuration.Pipelines)
                    {
                        PipelineDefinitions[configuration.Hash].Add(pipelineConfiguration.Identifier, BuildPipelineFromConfiguration(pipelineConfiguration));
                    }
                }

                //Log loaded pipeline definitions - what is loaded and with what configurations
                DDLogger.LogInfo<MainEngine>($"DokRouter engine will start with {PipelineDefinitions.Count} Configurations");
                foreach (var configurationHash in PipelineDefinitions.Keys)
                {
                    DDLogger.LogInfo<MainEngine>($"Configuration {configurationHash} has {PipelineDefinitions[configurationHash].Count} pipelines");

                    foreach (var pipelineDefinitionKey in PipelineDefinitions[configurationHash].Keys)
                    {
                        var pipelineDefinition = PipelineDefinitions[configurationHash][pipelineDefinitionKey];
                        DDLogger.LogInfo<MainEngine>($" - {pipelineDefinition.Name}: {pipelineDefinition.Identifier} with {pipelineDefinition.Activities.Count} activities");
                        
                        foreach(var activityDefinition in pipelineDefinition.Activities)
                        {
                            DDLogger.LogInfo<MainEngine>($"   - {activityDefinition.Name}: {activityDefinition.Identifier} with execution kind {activityDefinition.ExecutionDefinition.Kind}");
                        }
                    }
                }

                //TODO: Launch TEST pipeline for each pipeline definition

                //Log loaded running instances - what was loaded and with what configurations
                if (RunningInstances.Any())
                {
                    DDLogger.LogInfo<MainEngine>($"DokRouter engine detected {RunningInstances.Count} Running Instances - will trigger the start activity for the current activity of each of those instances");

                    Parallel.ForEach(RunningInstances, runningInstance =>
                    {
                        StartActivity(new StartActivityIn()
                        {
                            PipelineInstanceKey = runningInstance.Key
                        });
                    });
                }
                else
                {
                    DDLogger.LogInfo<MainEngine>($"DokRouter engine did not detect any Running Instances");
                }

                DDLogger.LogInfo<MainEngine>($"DokRouter engine started successfully!");
            }
        }

        /// <summary>
        /// Private method to fill the hash of the configuration, will depend recursivelly on the fill hash methods of the pipelines and activities
        /// </summary>
        private static void FillConfigurationHash(DokRouterEngineConfiguration latestEngineConfiguration)
        {
            foreach (var pipelineConfiguration in latestEngineConfiguration.Pipelines)
            {
                FillConfigurationHash(pipelineConfiguration);
            }

            var hashKey = $"{latestEngineConfiguration.DefaultPipelineIdentifier}|{latestEngineConfiguration.OnStartActivityAssembly}|{latestEngineConfiguration.OnStartActivityClass}|{latestEngineConfiguration.OnStartActivityMethod}|{string.Join("|", latestEngineConfiguration.Pipelines.Select(p => p.Hash))}";
            latestEngineConfiguration.Hash = DocDigitizer.Common.Security.Crypto.Hashing.MD5Hashing.SingletonMD5Hasher.Instance.Hash(hashKey);
        }

        /// <summary>
        /// Private method to fill the hash of the pipeline configuration, will depend recursivelly on the fill hash method of the activities
        /// </summary>
        private static void FillConfigurationHash(EDDokRouterEngineConfiguration_Pipelines pipelineConfiguration)
        {
            foreach(var pipelineActivity in pipelineConfiguration.Activities)
            {
                FillConfigurationHash(pipelineActivity);
            }

            var hashKey = $"{pipelineConfiguration.Name}|{pipelineConfiguration.Identifier}|{string.Join("|", pipelineConfiguration.Activities.Select(pa => pa.Hash))}";
            pipelineConfiguration.Hash = DocDigitizer.Common.Security.Crypto.Hashing.MD5Hashing.SingletonMD5Hasher.Instance.Hash(hashKey);
        }

        /// <summary>
        /// Private method to fill the hash of the activity configuration
        /// </summary>
        private static void FillConfigurationHash(EDDokRouterEngineConfiguration_Activities pipelineActivity)
        {
            var hashKey = $"{pipelineActivity.Name}|{pipelineActivity.Identifier}|{pipelineActivity.OrderNumber}|{pipelineActivity.Disabled}|{pipelineActivity.Kind}|{pipelineActivity.DirectActivityAssembly}|{pipelineActivity.DirectActivityClass}|{pipelineActivity.DirectActivityMethod}|{pipelineActivity.Url}|{pipelineActivity.KafkaTopic}";
            pipelineActivity.Hash = DocDigitizer.Common.Security.Crypto.Hashing.MD5Hashing.SingletonMD5Hasher.Instance.Hash(hashKey);
        }

        /// <summary>
        /// Builds a pipeline definiton based on the configuration
        /// </summary>
        /// <param name="pipelineConfiguration"></param>
        /// <returns></returns>
        private static PipelineDefinition BuildPipelineFromConfiguration(EDDokRouterEngineConfiguration_Pipelines pipelineConfiguration)
        {
            return new PipelineDefinition()
            {
                Name = pipelineConfiguration.Name,
                Identifier = pipelineConfiguration.Identifier,
                Activities = pipelineConfiguration.Activities.FindAll(a => !a.Disabled).Select(a => BuildActivityFromConfiguration(a)).OrderBy(a => a.OrderNumber).ToList()
            };
        }

        /// <summary>
        /// Builds an activity definition based on the configuration
        /// </summary>
        /// <param name="activityConfiguration"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="Exception"></exception>
        private static PipelineActivityDefinition BuildActivityFromConfiguration(EDDokRouterEngineConfiguration_Activities activityConfiguration)
        {
            ActivityExecutionDefinition executionDefinition = new ActivityExecutionDefinition();
            
            try
            {
                executionDefinition.Kind = activityConfiguration.Kind;
                switch(executionDefinition.Kind)
                {
                    case ActivityKind.Direct:
                        var onExecuteAssembly = Assembly.Load(activityConfiguration.DirectActivityAssembly);
                        if (onExecuteAssembly == null) { throw new Exception($"DirectActivityAssembly assembly '{activityConfiguration.DirectActivityAssembly}' not found"); }
                        var OnExecuteClass = onExecuteAssembly.GetType(activityConfiguration.DirectActivityClass);
                        if (OnExecuteClass == null) { throw new Exception($"DirectActivityClass type '{activityConfiguration.DirectActivityClass}' in assembly '{activityConfiguration.DirectActivityAssembly}' not found"); }
                        var onMessageMethod = OnExecuteClass.GetMethod(activityConfiguration.DirectActivityMethod);
                        if (onMessageMethod == null) { throw new Exception($"DirectActivityMethod method '{activityConfiguration.DirectActivityMethod}' in class '{activityConfiguration.DirectActivityClass}' not found"); }

                        try
                        {
                            executionDefinition.DirectActivityHandler = (OnExecuteActivityHandler)Delegate.CreateDelegate(typeof(OnExecuteActivityHandler), onMessageMethod);
                        }
                        catch(Exception ex)
                        {
                            throw new Exception($"Unable to load DirectActivityHandler configured with method '{activityConfiguration.DirectActivityMethod}' in class '{activityConfiguration.DirectActivityClass}'", ex);
                        }

                        break;

                    case ActivityKind.HTTP:
                        executionDefinition.Url = activityConfiguration.Url;
                        if (String.IsNullOrWhiteSpace(executionDefinition.Url))
                        {
                            throw new Exception("Activity is configured as HTTP but no Url is defined");
                        }
                        break;

                    case ActivityKind.KafkaEvent:
                        executionDefinition.KafkaTopic = activityConfiguration.KafkaTopic;
                        if (String.IsNullOrWhiteSpace(executionDefinition.KafkaTopic))
                        {
                            throw new Exception("Activity is configured as KafkaEvent but no KafkaTopic is defined");
                        }
                        break;

                    default:
                        throw new NotImplementedException("Activity kind is unknown or not implemented");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading activity {activityConfiguration.Name} from configuration", ex);
            }

            return new PipelineActivityDefinition()
            {
                Name = activityConfiguration.Name,
                Identifier = activityConfiguration.Identifier,
                OrderNumber = activityConfiguration.OrderNumber,

                ExecutionDefinition = executionDefinition
            };
        }

        /// <summary>
        /// Starts a pipeline with the given payload, will start the default pipeline if no pipeline is specified
        /// </summary>
        /// <param name="startPipelinePayload"></param>
        public static void StartPipeline(StartPipeline startPipelinePayload)
        {
            //Get pipeline to start with fallback to default pipeline
            var pipelineDefinitionIdToStart = startPipelinePayload?.PipelineDefinitionIdentifier ?? DefaultPipelineIdentifier;
            
            if(!pipelineDefinitionIdToStart.HasValue) 
            {
                DDLogger.LogError<MainEngine>("No pipeline to start, either pass a valid pipeline definition identifier or configure the default one");
                return;
            }

            if (PipelineDefinitions[LatestConfigurationHash].TryGetValue(pipelineDefinitionIdToStart.Value, out var pipelineDefinition))
            {
                InnerStartPipeline(pipelineDefinition, startPipelinePayload);
            }
            else
            {
                DDLogger.LogError<MainEngine>($"Undefined or not configured pipeline to start: {pipelineDefinitionIdToStart}");
                return;
            }
        }

        /// <summary>
        /// Moves a pipeline instance to an errored state
        /// </summary>
        public static void ErrorPipeline(PipelineInstance erroredPipeline, string errorMessage)
        {
            erroredPipeline.ErroredAt = DateTime.UtcNow;
            erroredPipeline.ErrorMessage = errorMessage;
            DokRouterDAL.ErrorPipelineInstance(erroredPipeline);
            RunningInstances.TryRemove(erroredPipeline.Key, out _);
            DDLogger.LogError<MainEngine>($"Pipeline instance {erroredPipeline.Key.PipelineInstanceIdentifier} errored with message: {errorMessage}. It was moved to the error collection and removed from running instances");
        }
        

        /// <summary>
        /// Starts a pipeline given its definition and the given payload
        /// </summary>
        /// <param name="pipelineDefinition"></param>
        /// <param name="startPipelinePayload"></param>
        private static void InnerStartPipeline(PipelineDefinition pipelineDefinition, StartPipeline startPipelinePayload)
        {
            //Create new instance
            var pipelineInstance = new PipelineInstance()
            {
                Key = new PipelineInstanceKey()
                {
                    ConfigurationHash = LatestConfigurationHash,
                    PipelineDefinitionIdentifier = pipelineDefinition.Identifier,
                    PipelineInstanceIdentifier = Guid.NewGuid()
                },
                CurrentActivityIndex = 0,
                StartedAt = DateTime.UtcNow,
                MarshalledExternalData = startPipelinePayload.MarshalledExternalData,

                ActivityExecutions = new Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>>()
            };

            //Add to running instances
            RunningInstances.TryAdd(pipelineInstance.Key, pipelineInstance);

            //Persist to DB
            DokRouterDAL.SaveOrUpdatePipelineInstance(pipelineInstance);

            DDLogger.LogInfo<MainEngine>($"Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) started new instance with identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

            //Trigger start of first activity
            StartActivity(new StartActivityIn()
            {
                PipelineInstanceKey = pipelineInstance.Key
            });
        }

        /// <summary>
        /// Starts an activity with the given payload
        /// </summary>
        /// <param name="startActivityPayload"></param>
        public static void StartActivity(StartActivityIn startActivityPayload)
        {
            if (!PipelineDefinitions[startActivityPayload.PipelineInstanceKey.ConfigurationHash].TryGetValue(startActivityPayload.PipelineInstanceKey.PipelineDefinitionIdentifier, out var pipelineDefinition))
            {
                DDLogger.LogError<MainEngine>($"No pipeline definition found for identifier: {startActivityPayload.PipelineInstanceKey.PipelineDefinitionIdentifier}");
                return;
            }            

            if (!RunningInstances.TryGetValue(startActivityPayload.PipelineInstanceKey, out var pipelineInstance))
            {
                DDLogger.LogError<MainEngine>($"Cannot find running instance: {startActivityPayload.PipelineInstanceKey.PipelineInstanceIdentifier} for pipeline definition: {pipelineDefinition} ({pipelineDefinition.Identifier})");
                return;
            }

            if (pipelineInstance.CurrentActivityIndex >= pipelineDefinition.Activities.Count)
            {
                DDLogger.LogError<MainEngine>($"Current index overflow, cannot start activity index {pipelineInstance.CurrentActivityIndex} for pipeline {pipelineDefinition} ({pipelineDefinition.Identifier}) as it only defines {pipelineDefinition.Activities.Count} activities");
                return;
            }

            var activityDefinition = pipelineDefinition.Activities[pipelineInstance.CurrentActivityIndex];
            DDLogger.LogDebug<MainEngine>($"Starting activity {activityDefinition.Name} ({activityDefinition.Identifier}) in Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

            if (!pipelineInstance.ActivityExecutions.ContainsKey(pipelineInstance.CurrentActivityIndex)) { pipelineInstance.ActivityExecutions.Add(pipelineInstance.CurrentActivityIndex, new Dictionary<ActivityExecutionKey, ActivityExecution>()); }

            var activityExecutionKey = new ActivityExecutionKey()
            {
                PipelineInstanceKey = pipelineInstance.Key,
                ActivityDefinitionIdentifier = activityDefinition.Identifier,
                ActivityExecutionIdentifier = Guid.NewGuid()
            };

            pipelineInstance.ActivityExecutions[pipelineInstance.CurrentActivityIndex].Add(activityExecutionKey, new ActivityExecution()
            {
                Key = activityExecutionKey,
                StartedAt = DateTime.UtcNow
            });

            Task.Run(() =>
            {
                OnStartActivityHandlers[startActivityPayload.PipelineInstanceKey.ConfigurationHash](activityDefinition.ExecutionDefinition, new StartActivityOut()
                {
                    ActivityExecutionKey = activityExecutionKey,
                    MarshalledExternalData = pipelineInstance.MarshalledExternalData
                });
            });

            DDLogger.LogInfo<MainEngine>($"Started activity {activityDefinition.Name} ({activityDefinition.Identifier}) in Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

            //Persist to DB
            DokRouterDAL.SaveOrUpdatePipelineInstance(pipelineInstance);
            //EPocas, 11-04-2024 - Check if needed as recovery only requires the pipeline instance
            //DokRouterDAL.StartActivityExecution(activityExecutionKey);
        }

        /// <summary>
        /// Ends an activity with the given payload, will increment the current activity index and start the next activity if available
        /// If no more activities will finish the pipeline instance
        /// </summary>
        public static void EndActivity(EndActivity endActivityPayload)
        {
            if (!PipelineDefinitions[endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.ConfigurationHash].TryGetValue(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineDefinitionIdentifier, out var pipelineDefinition))
            {
                DDLogger.LogError<MainEngine>($"No pipeline definition found for identifier: {endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineDefinitionIdentifier}");
                return;
            }

            if (!RunningInstances.TryGetValue(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey, out var pipelineInstance))
            {
                DDLogger.LogError<MainEngine>($"Cannot find running instance: {endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineInstanceIdentifier} for pipeline definition: {pipelineDefinition} ({pipelineDefinition.Identifier})");
                return;
            }
            
            //Used for logging and for detecting next activity
            var activityDefinition = pipelineDefinition.Activities[pipelineInstance.CurrentActivityIndex];

            var activityExecutionKey = endActivityPayload.ActivityExecutionKey;
            if (pipelineInstance.ActivityExecutions.ContainsKey(pipelineInstance.CurrentActivityIndex) && pipelineInstance.ActivityExecutions[pipelineInstance.CurrentActivityIndex].ContainsKey(activityExecutionKey))
            {
                pipelineInstance.ActivityExecutions[pipelineInstance.CurrentActivityIndex][activityExecutionKey].EndedAt = DateTime.UtcNow;
                pipelineInstance.ActivityExecutions[pipelineInstance.CurrentActivityIndex][activityExecutionKey].IsSuccess = endActivityPayload.IsSuccess;
                pipelineInstance.ActivityExecutions[pipelineInstance.CurrentActivityIndex][activityExecutionKey].ErrorMessage = endActivityPayload.ErrorMessage;
            }

            //Update model
            pipelineInstance.MarshalledExternalData = endActivityPayload.MarshalledExternalData;

            //Move to next activity
            pipelineInstance.CurrentActivityIndex++;

            //Persist to DB - Finished activity
            DokRouterDAL.SaveOrUpdatePipelineInstance(pipelineInstance);
            //EPocas, 11-04-2024 - Check if needed as recovery only requires the pipeline instance
            //DokRouterDAL.EndActivityExecution(endActivityPayload.ActivityExecutionKey);

            DDLogger.LogInfo<MainEngine>($"Ended activity {activityDefinition.Name} ({activityDefinition.Identifier}) in Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

            //Check if there are activities available
            if (pipelineInstance.CurrentActivityIndex < pipelineDefinition.Activities.Count)
            {
                StartActivity(new StartActivityIn()
                {
                    PipelineInstanceKey = endActivityPayload.ActivityExecutionKey.PipelineInstanceKey
                });
            }
            else
            {
                //Reached the end of the pipeline
                DDLogger.LogInfo<MainEngine>($"Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) finished instance with identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

                //Remove from running instances
                RunningInstances.TryRemove(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey, out _);

                //Persist to DB - Finished pipeline
                pipelineInstance.FinishedAt = DateTime.UtcNow;
                DokRouterDAL.FinishPipelineInstance(pipelineInstance);
            }
        }
    }
}
