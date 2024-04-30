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
using Joyn.Timelog.Common.Models;

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
        /// Dictionary to hold lockers for each pipeline instance, indexed by their key
        /// </summary>
        private static ConcurrentDictionary<PipelineInstanceKey, ReaderWriterLockSlim> PipelineInstancesLocker = new ConcurrentDictionary<PipelineInstanceKey, ReaderWriterLockSlim>();


        /// <summary>
        /// Dictionary to hold log messages that were started but not yet finished, indexed by their key
        /// </summary>
        private static ConcurrentDictionary<Guid, LogMessage> StartedLogMessages = new ConcurrentDictionary<Guid, LogMessage>();

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
                        PipelineDefinitions[configuration.Hash].Add(pipelineConfiguration.Identifier, BuildPipelineFromConfiguration(pipelineConfiguration, configuration.CommonConfigurations));
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
                        PipelineInstancesLocker.TryAdd(runningInstance.Key, new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion));
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

            var hashKey = $"{latestEngineConfiguration.DefaultPipelineIdentifier}|{latestEngineConfiguration.OnStartActivityAssembly}|{latestEngineConfiguration.OnStartActivityClass}|{latestEngineConfiguration.OnStartActivityMethod}|{string.Join("|", latestEngineConfiguration.Pipelines.Select(p => p.Hash))}|{GetCommonConfigurationHash(latestEngineConfiguration.CommonConfigurations)}";
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

            var hashKey = $"{pipelineConfiguration.Name}|{pipelineConfiguration.Identifier}|{string.Join("|", pipelineConfiguration.Activities.Select(pa => pa.Hash))}|{GetCommonConfigurationHash(pipelineConfiguration.CommonConfigurations)}";
            pipelineConfiguration.Hash = DocDigitizer.Common.Security.Crypto.Hashing.MD5Hashing.SingletonMD5Hasher.Instance.Hash(hashKey);
        }

        /// <summary>
        /// Private method to fill the hash of the activity configuration
        /// </summary>
        private static void FillConfigurationHash(EDDokRouterEngineConfiguration_Activities pipelineActivity)
        {
            var hashKey = $"{pipelineActivity.Name}|{pipelineActivity.Identifier}|{pipelineActivity.OrderNumber}|{pipelineActivity.Disabled}|{pipelineActivity.Kind}|{pipelineActivity.DirectActivityAssembly}|{pipelineActivity.DirectActivityClass}|{pipelineActivity.DirectActivityMethod}|{pipelineActivity.Url}|{pipelineActivity.KafkaTopic}|{GetCommonConfigurationHash(pipelineActivity.CommonConfigurations)}";
            pipelineActivity.Hash = DocDigitizer.Common.Security.Crypto.Hashing.MD5Hashing.SingletonMD5Hasher.Instance.Hash(hashKey);
        }

        private static string GetCommonConfigurationHash(CommonConfigurations commonConfiguration)
        {
            var hashKey = "";
            if (commonConfiguration != null)
            {
                hashKey = $"{commonConfiguration.ActivitySLATimeInSeconds}|{commonConfiguration.ActivityTrySLATimeInSeconds}|{commonConfiguration.RetryOnSLAExpired}|{commonConfiguration.RetryOnSLAExpiredMaxRetries}|{commonConfiguration.RetryOnSLAExpiredDelayInSeconds}|{commonConfiguration.RetryOnError}|{commonConfiguration.RetryOnErrorMaxRetries}|{commonConfiguration.RetryOnErrorDelayInSeconds}";
            }
            return DocDigitizer.Common.Security.Crypto.Hashing.MD5Hashing.SingletonMD5Hasher.Instance.Hash(hashKey);
        }

        /// <summary>
        /// Builds a pipeline definiton based on the configuration
        /// </summary>
        /// <param name="pipelineConfiguration"></param>
        /// <returns></returns>
        private static PipelineDefinition BuildPipelineFromConfiguration(EDDokRouterEngineConfiguration_Pipelines pipelineConfiguration, CommonConfigurations configurationCommonConfigurations)
        {
            return new PipelineDefinition()
            {
                Name = pipelineConfiguration.Name,
                Identifier = pipelineConfiguration.Identifier,
                Activities = pipelineConfiguration.Activities.FindAll(a => !a.Disabled).Select(a => BuildActivityFromConfiguration(a, configurationCommonConfigurations, pipelineConfiguration.CommonConfigurations)).OrderBy(a => a.OrderNumber).ToList(),

                //Common configurations loading with overriding logic from Default -> Engine -> Pipeline -> Activity
                CommonConfigurations = CommonConfigurations.DefaultCommonConfigurations.Clone()
                                                                                       .Override(configurationCommonConfigurations)
                                                                                       .Override(pipelineConfiguration.CommonConfigurations)
            };
        }

        /// <summary>
        /// Builds an activity definition based on the configuration
        /// </summary>
        /// <param name="activityConfiguration"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="Exception"></exception>
        private static PipelineActivityDefinition BuildActivityFromConfiguration(EDDokRouterEngineConfiguration_Activities activityConfiguration, CommonConfigurations configurationCommonConfigurations, CommonConfigurations pipelineConfigurations)
        {
            ActivityExecutionDefinition executionDefinition = new ActivityExecutionDefinition();
            
            try
            {
                //Kind of activity and corresponding specific definition loading
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

            //Common configurations loading with overriding logic from Default -> Engine -> Pipeline -> Activity
            var commonConfigurationToApply = CommonConfigurations.DefaultCommonConfigurations.Clone()
                                                                                             .Override(configurationCommonConfigurations)
                                                                                             .Override(pipelineConfigurations)
                                                                                             .Override(activityConfiguration.CommonConfigurations);

            return new PipelineActivityDefinition()
            {
                Name = activityConfiguration.Name,
                Identifier = activityConfiguration.Identifier,
                OrderNumber = activityConfiguration.OrderNumber,

                ExecutionDefinition = executionDefinition,
                CommonConfigurations = commonConfigurationToApply
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
            PipelineInstancesLocker.TryGetValue(erroredPipeline.Key, out var locker);
            if (locker == null)
            {
                //Pipeline not running anymore
                DDLogger.LogWarn<MainEngine>($"Pipeline instance {erroredPipeline.Key} not found in running instances - Request for ErrorPipeline will be discarded");
                return;
            }
            locker.EnterWriteLock();
            try
            {
                erroredPipeline.ErroredAt = DateTime.UtcNow;
                erroredPipeline.ErrorMessage = errorMessage;
                DokRouterDAL.ErrorPipelineInstance(erroredPipeline);
                RunningInstances.TryRemove(erroredPipeline.Key, out _);
                PipelineInstancesLocker.TryRemove(erroredPipeline.Key, out _);
                DDLogger.LogError<MainEngine>($"Pipeline instance {erroredPipeline.Key.PipelineInstanceIdentifier} errored with message: {errorMessage}. It was moved to the error collection and removed from running instances");
            }
            finally
            {
                locker.ExitWriteLock();
            }
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
                TransactionIdentifier = startPipelinePayload.TransactionIdentifier ?? Guid.NewGuid(), //Should the new one follow some pattern so we know it was generated here? Like 0000c4a3-2cdd-40c5-9227-85d290fbfa28

                CurrentActivityIndex = 0,
                Name = pipelineDefinition.Name,
                StartedAt = DateTime.UtcNow,
                PipelineSLAMoment = DateTime.UtcNow.AddSeconds(pipelineDefinition.CommonConfigurations.PipelineSLATimeInSeconds ?? CommonConfigurations.DefaultCommonConfigurations.PipelineSLATimeInSeconds.Value),
                MarshalledExternalData = startPipelinePayload.MarshalledExternalData,

                ActivityInstances = new Dictionary<int, ActivityInstance>()
            };

            //Add to running instances
            RunningInstances.TryAdd(pipelineInstance.Key, pipelineInstance);
            PipelineInstancesLocker.TryAdd(pipelineInstance.Key, new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion));

            //Persist to DB
            DokRouterDAL.SaveOrUpdatePipelineInstance(pipelineInstance);

            //Timelog start of the pipeline
            var startMessage = Timelog.Client.Logger.LogStart(Microsoft.Extensions.Logging.LogLevel.Information, JGTimelogDomainTable._51_Pipeline, pipelineInstance.TransactionIdentifier, pipelineInstance.Key.PipelineInstanceIdentifier, null);
            StartedLogMessages[pipelineInstance.Key.PipelineInstanceIdentifier] = startMessage;

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
            PipelineInstancesLocker.TryGetValue(startActivityPayload.PipelineInstanceKey, out var locker);
            if (locker == null)
            {
                //Pipeline not running anymore
                DDLogger.LogWarn<MainEngine>($"Pipeline instance {startActivityPayload.PipelineInstanceKey} not found in running instances - Request for StartActivity will be discarded");
                return;
            }
            locker.EnterWriteLock();
            try
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
                    ErrorPipeline(pipelineInstance, $"Inconsistent Pipeline: Current index overflow, cannot start activity index {pipelineInstance.CurrentActivityIndex}");
                    return;
                }

                var activityDefinition = pipelineDefinition.Activities[pipelineInstance.CurrentActivityIndex];
                DDLogger.LogDebug<MainEngine>($"Starting activity {activityDefinition.Name} ({activityDefinition.Identifier}) in Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

                //EPocas, 18-04-2024 - This code might be useful if we want parallel executions under the same step
                //if (!pipelineInstance.ActivityInstances.ContainsKey(pipelineInstance.CurrentActivityIndex)) 
                //{ 
                //    pipelineInstance.ActivityInstances.Add(pipelineInstance.CurrentActivityIndex, new Dictionary<Guid, ActivityInstance>()); 
                //}

                //if (!pipelineInstance.ActivityInstances[pipelineInstance.CurrentActivityIndex].ContainsKey(activityDefinition.Identifier))
                //{
                //    pipelineInstance.ActivityInstances[pipelineInstance.CurrentActivityIndex].Add(activityDefinition.Identifier, new ActivityInstance()
                //    {
                //        Name = activityDefinition.Name,
                //        StartedAt = DateTime.UtcNow,
                //        ActivitySLAMoment = DateTime.UtcNow.AddSeconds(activityDefinition.CommonConfigurations.ActivitySLATimeInSeconds ?? CommonConfigurations.DefaultCommonConfigurations.ActivitySLATimeInSeconds.Value),
                //        Executions = new List<ActivityExecution>()
                //    });
                //}

                if (!pipelineInstance.ActivityInstances.ContainsKey(pipelineInstance.CurrentActivityIndex))
                {
                    pipelineInstance.ActivityInstances.Add(pipelineInstance.CurrentActivityIndex, new ActivityInstance()
                    {
                        Name = activityDefinition.Name,
                        StartedAt = DateTime.UtcNow,
                        ActivitySLAMoment = DateTime.UtcNow.AddSeconds(activityDefinition.CommonConfigurations.ActivitySLATimeInSeconds ?? CommonConfigurations.DefaultCommonConfigurations.ActivitySLATimeInSeconds.Value),
                        Executions = new List<ActivityExecution>()
                    });
                }


                if (pipelineInstance.ActivityInstances[pipelineInstance.CurrentActivityIndex].Executions.Any())
                {
                    //Not first execution of activity, meaning we are doing some retry, aditional actions are needed

                    //All previous activity executions should be flagged as errored
                    foreach (var activityExecution in pipelineInstance.ActivityInstances[pipelineInstance.CurrentActivityIndex].Executions)
                    {
                        if (!activityExecution.EndedAt.HasValue)
                        {
                            activityExecution.EndedAt = DateTime.UtcNow;
                            activityExecution.IsSuccess = false;
                            activityExecution.ErrorMessage = "Another execution started before this one ended";
                        }
                    }

                    //Check if there are any retries available
                    if (pipelineInstance.ActivityInstances[pipelineInstance.CurrentActivityIndex].Executions.Count >= activityDefinition.CommonConfigurations.RetryOnSLAExpiredMaxRetries + 1)
                    {
                        //No more executions! Do not start the activity, terminate the pipeline instead
                        DDLogger.LogError<MainEngine>($"activity {activityDefinition.Name} ({activityDefinition.Identifier}) in Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier} reached limit of retries. Pipeline will be errored");
                        ErrorPipeline(pipelineInstance, $"Activity {activityDefinition.Name} reached limit of retries");
                        return;
                    }

                    //Check if retry obeys the delay
                    if (DateTime.UtcNow < pipelineInstance.ActivityInstances[pipelineInstance.CurrentActivityIndex].Executions.Last().EndedAt.Value.AddSeconds(activityDefinition.CommonConfigurations.RetryOnSLAExpiredDelayInSeconds ?? CommonConfigurations.DefaultCommonConfigurations.RetryOnSLAExpiredDelayInSeconds.Value))
                    {
                        //Retry not allowed yet, wait for the delay to pass
                        DDLogger.LogDebug<MainEngine>($"Activity {activityDefinition.Name} ({activityDefinition.Identifier}) in Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier} required retry within delay period");
                        return;
                    }
                }

                var activityExecutionKey = new ActivityExecutionKey()
                {
                    PipelineInstanceKey = pipelineInstance.Key,
                    ActivityDefinitionIdentifier = activityDefinition.Identifier,
                    ActivityExecutionIdentifier = Guid.NewGuid()
                };

                pipelineInstance.ActivityInstances[pipelineInstance.CurrentActivityIndex].Executions.Add(new ActivityExecution()
                {
                    Key = activityExecutionKey,
                    StartedAt = DateTime.UtcNow,
                    ActivityTrySLAMoment = DateTime.UtcNow.AddSeconds(activityDefinition.CommonConfigurations.ActivityTrySLATimeInSeconds ?? CommonConfigurations.DefaultCommonConfigurations.ActivityTrySLATimeInSeconds.Value),
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

                //Timelog start of the activity
                var startMessage = Timelog.Client.Logger.LogStart(Microsoft.Extensions.Logging.LogLevel.Information, JGTimelogDomainTable._51_Activity, pipelineInstance.TransactionIdentifier, activityExecutionKey.ActivityExecutionIdentifier, null);
                StartedLogMessages[activityExecutionKey.ActivityExecutionIdentifier] = startMessage;
            }
            finally
            {
                locker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Ends an activity with the given payload, will increment the current activity index and start the next activity if available
        /// If no more activities will finish the pipeline instance
        /// </summary>
        public static void EndActivity(EndActivity endActivityPayload)
        {
            PipelineInstancesLocker.TryGetValue(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey, out var locker);
            if (locker == null)
            {
                //Pipeline not running anymore
                DDLogger.LogWarn<MainEngine>($"Pipeline instance {endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineInstanceIdentifier} not found in running instances - Request for EndActivity will be discarded");
                return;
            }
            locker.EnterWriteLock();
            try
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
                if (pipelineInstance.ActivityInstances.ContainsKey(pipelineInstance.CurrentActivityIndex))
                {
                    var activityInstance = pipelineInstance.ActivityInstances[pipelineInstance.CurrentActivityIndex];
                    var activityExecution = pipelineInstance.ActivityInstances[pipelineInstance.CurrentActivityIndex].Executions.FirstOrDefault(e => e.Key.Equals(activityExecutionKey));

                    if (activityExecution == null)
                    {
                        DDLogger.LogError<MainEngine>($"Execution {activityExecutionKey.ActivityExecutionIdentifier} for activity {activityDefinition.Name} ({activityDefinition.Identifier}) in Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier} attempted to finish but was not found! Nothing will be done.");
                        return;
                    }

                    if (activityExecution.EndedAt.HasValue)
                    {
                        DDLogger.LogWarn<MainEngine>($"Execution {activityExecutionKey.ActivityExecutionIdentifier} for activity {activityDefinition.Name} ({activityDefinition.Identifier}) in Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier} attempted to finish but already ended! Nothing will be done.");
                        return;
                    }

                    if (activityInstance.EndedAt.HasValue)
                    {
                        DDLogger.LogWarn<MainEngine>($"Activity {activityDefinition.Name} ({activityDefinition.Identifier}) in Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier} attempted to finish but already ended! Nothing will be done.");
                        return;
                    }

                    activityInstance.EndedAt = DateTime.UtcNow;
                    activityInstance.IsSuccess = endActivityPayload.IsSuccess;
                    activityInstance.ErrorMessage = endActivityPayload.ErrorMessage;

                    activityExecution.EndedAt = DateTime.UtcNow;
                    activityExecution.IsSuccess = endActivityPayload.IsSuccess;
                    activityExecution.ErrorMessage = endActivityPayload.ErrorMessage;
                }

                //Update model
                pipelineInstance.MarshalledExternalData = endActivityPayload.MarshalledExternalData;

                //Move to next activity
                pipelineInstance.CurrentActivityIndex++;

                //Persist to DB - Finished activity
                DokRouterDAL.SaveOrUpdatePipelineInstance(pipelineInstance);
                //EPocas, 11-04-2024 - Check if needed as recovery only requires the pipeline instance
                //DokRouterDAL.EndActivityExecution(endActivityPayload.ActivityExecutionKey);

                //Timelog stop of the activity
                if(StartedLogMessages.TryGetValue(activityExecutionKey.ActivityExecutionIdentifier, out var startMessage))
                { Timelog.Client.Logger.LogStop(startMessage); }
                else 
                { Timelog.Client.Logger.LogStop(Microsoft.Extensions.Logging.LogLevel.Information, JGTimelogDomainTable._51_Activity, pipelineInstance.TransactionIdentifier, activityExecutionKey.ActivityExecutionIdentifier, null); }

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
                    PipelineInstancesLocker.TryRemove(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey, out _);

                    //Persist to DB - Finished pipeline
                    pipelineInstance.FinishedAt = DateTime.UtcNow;
                    DokRouterDAL.FinishPipelineInstance(pipelineInstance);

                    //Timelog stop of the pipeline
                    if(StartedLogMessages.TryGetValue(pipelineInstance.Key.PipelineInstanceIdentifier, out var startPipelineLogMessage))
                    { Timelog.Client.Logger.LogStop(startPipelineLogMessage); }
                    else
                    { Timelog.Client.Logger.LogStop(Microsoft.Extensions.Logging.LogLevel.Information, JGTimelogDomainTable._51_Pipeline, pipelineInstance.TransactionIdentifier, pipelineInstance.Key.PipelineInstanceIdentifier, null); }

                }
            }
            finally
            {
                locker.ExitWriteLock();
            }
        }
    }
}
