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
using System.Text.Json;
using DocDigitizer.Common.Extensions;

namespace Joyn.DokRouter
{
    /// <summary>
    /// The main engine of the DokRouter, responsible for starting and managing pipelines and their activities
    /// </summary>
    public class MainEngine
    {
        //TODO: MOVE TO CONFIGURATION
        private const int EngineDefaulMaxNumberCycles = 5;

        private static readonly object dokRouterEngineConfigurationLocker = new object();
        

        /// <summary>
        /// The default pipeline identifier to be used when no pipeline is specified
        /// </summary>
        private static Guid? DefaultPipelineIdentifier { get; set; }

        /// <summary>
        /// Dictionary of Activity Pool, indexed by their identifier. This will hold the latest version of each Activity
        /// </summary>
        private static Dictionary<Guid, ActivityDefinition> ActivityPool { get; set; } = new Dictionary<Guid, ActivityDefinition>();

        /// <summary>
        /// Dictionary of archived activity definitions that might still be needed, indexed by their identifier, and indexed by the hash of their configuration
        /// This dictionary will be loaded on demand as old configurations are needed
        /// First Key is the Activity Configuration Identifier - used to identify the activity
        /// Second Key is the Hash of the Activity Configuration - used to identify the configuration as we can have different configurations for the same activity
        /// </summary>
        private static Dictionary<Guid, Dictionary<string, ActivityDefinition>> ArchiveActivityPool { get; set; } = new Dictionary<Guid, Dictionary<string, ActivityDefinition>>();

        ///<summary>
        /// Dictionary of Pipeline Pool, indexed by their identifier. This will hold the latest version of each Pipeline
        /// </summary>
        private static Dictionary<Guid, PipelineDefinition> PipelinePool { get; set; } = new Dictionary<Guid, PipelineDefinition>();

        /// <summary>
        /// Dictionary of archived pipeline definitions, indexed by their identifier, and indexed by the hash of their configuration
        /// This dictionary will be loaded on demand as old configurations are needed
        /// First Key is the Pipeline Configuration Identifier - used to identify the pipeline
        /// Second Key is the Hash of the Pipeline Configuration - used to identify the configuration as we can have different configurations for the same pipeline
        /// </summary>
        private static Dictionary<Guid, Dictionary<string, PipelineDefinition>> ArchivePipelinePool { get; set; } = new Dictionary<Guid, Dictionary<string, PipelineDefinition>>();


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
        /// Common configurations to be applied when activity or pipelines does not define their own
        /// </summary>
        private static CommonConfigurations CommonConfigurations { get; set; }

        /// <summary>
        /// Start up method for the engine, will load the configuration and start the engine, must be invoked prior to any other method
        /// </summary>
        public static void Startup(IDokRouterDAL dokRouterDAL, string endActivityCallbackUrl)
        {
            lock (dokRouterEngineConfigurationLocker)
            {
                DDLogger.LogInfo<MainEngine>($"DokRouter engine is starting...");

                DokRouterDAL = dokRouterDAL;

                ActivityStarter.Startup(endActivityCallbackUrl);
                
                #region Common Configurations

                DDLogger.LogInfo<MainEngine>($"DokRouter loading common configurations...");

                CommonConfigurations = DokRouterDAL.GetCommonConfigurations();

                #endregion

                #region Activity Configurations and Activity Definition Pool StartUp

                DDLogger.LogInfo<MainEngine>($"DokRouter loading activity pool...");

                //Get latest configurations
                var activityConfigurations = DokRouterDAL.GetActivityConfigurations();
                if (activityConfigurations == null) { throw new Exception("Unable to Get Activity Configurations from the DAL Implementation"); }
                if(!activityConfigurations.Any()) { throw new Exception("No Activity Configurations where returned from the DAL Implementation. Cannot Start Engine without an Activity Pool"); }

                foreach(var activityConfiguration in activityConfigurations)
                {
                    if(activityConfiguration == null)
                    {
                        DDLogger.LogError<MainEngine>("DokRouterDAL returned a null activity configuration. Check the repository for inconsistencies");
                        continue;
                    }
                    //Calculate hash of the activity configuration
                    var serializedActivityConfiguration = JsonSerializer.Serialize(activityConfiguration);
                    activityConfiguration.Hash = DocDigitizer.Common.Security.Crypto.Hashing.MD5Hashing.SingletonMD5Hasher.Instance.Hash(serializedActivityConfiguration);
                    
                    //Add to the archive configuration if it doesn't exist
                    var existingArchiveActivityConfiguration = DokRouterDAL.GetArchiveActivityConfigurationByHash(activityConfiguration.Hash);
                    if (existingArchiveActivityConfiguration == null)
                    {
                        DDLogger.LogInfo<MainEngine>($"Detected new activity configuration with Hash: {activityConfiguration.Hash}, related to activity '{activityConfiguration.Name}', persisting so it can be reused if needed");
                        DokRouterDAL.SaveOrUpdateActivityConfigurationArchive(activityConfiguration);
                    }

                    //Load activity definition based on configuration and add it to the activity pool
                    ActivityDefinition activityDefinition = BuildActivityDefinitionFromConfiguration(activityConfiguration);
                    ActivityPool.Add(activityDefinition.Configuration.Identifier, activityDefinition);
                }

                #endregion

                #region Pipeline Configurations and Pipeline Definition Pool StartUp

                DDLogger.LogInfo<MainEngine>($"DokRouter loading pipeline pool...");

                //Get latest configurations
                var pipelineConfigurations = DokRouterDAL.GetPipelineConfigurations();
                if (pipelineConfigurations == null) { throw new Exception("Unable to Get Pipeline Configurations from the DAL Implementation"); }
                if (!pipelineConfigurations.Any()) { throw new Exception("No Pipeline Configurations where returned from the DAL Implementation. Cannot Start Engine without a Pipeline Pool"); }

                foreach (var pipelineConfiguration in pipelineConfigurations)
                {
                    if (pipelineConfiguration == null)
                    {
                        DDLogger.LogError<MainEngine>("DokRouterDAL returned a null pipeline configuration. Check the repository for inconsistencies");
                        continue;
                    }

                    //Calculate hash of the pipeline configuration
                    var serializedPipelineConfiguration = JsonSerializer.Serialize(pipelineConfiguration);
                    pipelineConfiguration.Hash = DocDigitizer.Common.Security.Crypto.Hashing.MD5Hashing.SingletonMD5Hasher.Instance.Hash(serializedPipelineConfiguration);

                    //Add to the archive configuration if it doesn't exist
                    var existingArchivePipelineConfiguration = DokRouterDAL.GetArchivePipelineConfigurationByHash(pipelineConfiguration.Hash);
                    if (existingArchivePipelineConfiguration == null)
                    {
                        DDLogger.LogInfo<MainEngine>($"Detected new pipeline configuration with Hash: {pipelineConfiguration.Hash}, related to pipeline '{pipelineConfiguration.Name}', persisting so it can be reused if needed");
                        DokRouterDAL.SaveOrUpdatePipelineConfigurationArchive(pipelineConfiguration);
                    }

                    //Load pipeline definition based on configuration and add it to the activity pool
                    PipelineDefinition pipelineDefinition = BuildPipelineDefinitionFromConfiguration(pipelineConfiguration);
                    PipelinePool.Add(pipelineDefinition.Configuration.Identifier, pipelineDefinition);

                    //Check if pipeline has trigger automatism
                    if(pipelineConfiguration.Trigger != null)
                    {
                        PipelineTriggerInstance pipelineTriggerInstance = new PipelineTriggerInstance()
                        {
                            Identifier = Guid.NewGuid(),
                            ConfigurationIdentifier = pipelineConfiguration.Trigger.Identifier,
                            PipelineIdentifier = pipelineConfiguration.Identifier,

                            PreConditionActivity = pipelineConfiguration.Trigger.PreConditionActivityIdentifier.HasValue ? ActivityPool.TryGetAndReturnValue(pipelineConfiguration.Trigger.PreConditionActivityIdentifier.Value) : null,
                            ExpectedPreConditionField = pipelineConfiguration.Trigger.ExpectedPreConditionField,

                            NextExecution = DateTime.UtcNow, //Initialize to now so it will trigger the first time
                            Kind = pipelineConfiguration.Trigger.Kind,
                            TimeFrequencySeconds = pipelineConfiguration.Trigger.TimeFrequencySeconds
                        };

                        //Validations of the trigger configuration
                        if(pipelineTriggerInstance.PreConditionActivity == null && pipelineConfiguration.Trigger.PreConditionActivityIdentifier.HasValue)
                        {
                            DDLogger.LogWarn<MainEngine>($"PreConditionActivity for pipeline trigger {pipelineTriggerInstance.Identifier} not found in the activity pool. Check the configuration of the pipeline trigger {pipelineConfiguration.Trigger.Identifier}. Will continue without pre condition activity");
                            continue;
                        }

                        if (pipelineTriggerInstance.PreConditionActivity != null && string.IsNullOrWhiteSpace(pipelineTriggerInstance.ExpectedPreConditionField))
                        {
                            DDLogger.LogWarn<MainEngine>($"PreConditionActivity was loaded for {pipelineTriggerInstance.Identifier} but no ExpectedPreConditionField was given. Will continue without pre condition activity");
                            pipelineTriggerInstance.PreConditionActivity = null;
                            continue;
                        }

                        switch (pipelineConfiguration.Trigger.Kind)
                        {
                            case PipelineTriggerKind.TimerFrequency:
                                if (!pipelineTriggerInstance.TimeFrequencySeconds.HasValue)
                                {
                                    DDLogger.LogWarn<MainEngine>($"Pipeline Trigger {pipelineTriggerInstance.Identifier} was configured with Kind TimerFrequency but without TimeFrequencySeconds. Trigger will be discarded");
                                    break;
                                }
                                EngineTriggering.RegisterPipelineTrigger(pipelineTriggerInstance);
                                break;

                            default:
                                DDLogger.LogError<MainEngine>($"Unknown or not implemented pipeline trigger kind: {pipelineConfiguration.Trigger.Kind.ToString()}");
                                break;
                        }
                    }   
                }

                #endregion

                //Load Running instances from DB 
                var runningInstances = DokRouterDAL.GetRunningInstances();

                foreach (var runningInstance in runningInstances)
                {
                    RunningInstances.TryAdd(runningInstance.Key, runningInstance);
                    PipelineInstancesLocker.TryAdd(runningInstance.Key, new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion));
                }

                ////Log loaded pipeline definitions - what is loaded and with what configurations
                DDLogger.LogInfo<MainEngine>($"DokRouter engine will start with the following pools");

                DDLogger.LogInfo<MainEngine>($"{PipelinePool.Count} Pipeline Configurations:");
                
                foreach (var pipelineDefinition in PipelinePool.Values)
                {
                    DDLogger.LogInfo<MainEngine>($" - {pipelineDefinition.Configuration.Name}: {pipelineDefinition.Configuration.Identifier}");
                }

                DDLogger.LogInfo<MainEngine>($"{PipelinePool.Count} Activity Configurations:");

                foreach (var activityDefinition in ActivityPool.Values)
                {
                    DDLogger.LogInfo<MainEngine>($" - {activityDefinition.Configuration.Name}: {activityDefinition.Configuration.Identifier} with execution kind {activityDefinition.Configuration.KindText}");
                }

                ////TODO: Launch TEST pipeline for each pipeline definition

                //Log loaded running instances - what was loaded and with what configurations
                if (RunningInstances.Any())
                {
                    DDLogger.LogInfo<MainEngine>($"DokRouter engine detected {RunningInstances.Count} Running Instances - will trigger the start activity for the current activity of each of those instances");

                    Parallel.ForEach(RunningInstances, runningInstance =>
                    {
                        try
                        {
                            PipelineInstancesLocker.TryAdd(runningInstance.Key, new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion));
                            StartActivity(new StartActivityIn()
                            {
                                PipelineInstanceKey = runningInstance.Key
                            });
                        }
                        catch (Exception ex)
                        {
                            //Prevent activities in error to block the engine
                            DDLogger.LogException<MainEngine>($"Error (re)starting activity for running instance {runningInstance.Key.PipelineInstanceIdentifier}", ex);
                        }
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
        /// Builds an activity definition based on the activity configuration
        /// </summary>
        private static ActivityDefinition BuildActivityDefinitionFromConfiguration(ActivityConfiguration activityConfiguration)
        {
            ActivityDefinition activityDefinition = new ActivityDefinition() { Configuration = activityConfiguration };

            try
            {
                //Kind of activity and corresponding specific definition loading
                switch (activityConfiguration.Kind)
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
                            activityDefinition.DirectActivityHandler = (OnExecuteActivityHandler)Delegate.CreateDelegate(typeof(OnExecuteActivityHandler), onMessageMethod);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Unable to load DirectActivityHandler configured with method '{activityConfiguration.DirectActivityMethod}' in class '{activityConfiguration.DirectActivityClass}'", ex);
                        }

                        break;

                    case ActivityKind.HTTP:
                        activityDefinition.Url = activityConfiguration.Url;
                        if (String.IsNullOrWhiteSpace(activityDefinition.Url))
                        {
                            throw new Exception("Activity is configured as HTTP but no Url is defined");
                        }
                        break;

                    case ActivityKind.KafkaEvent:
                        activityDefinition.KafkaTopic = activityConfiguration.KafkaTopic;
                        if (String.IsNullOrWhiteSpace(activityDefinition.KafkaTopic))
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
                                                                                             .Override(CommonConfigurations)
                                                                                             .Override(activityConfiguration.CommonConfigurations);

            activityDefinition.CommonConfigurations = commonConfigurationToApply;

            return activityDefinition;
        }

        private static PipelineDefinition BuildPipelineDefinitionFromConfiguration(PipelineConfiguration pipelineConfiguration)
        {
            PipelineDefinition pipelineDefinition = new PipelineDefinition() { Configuration = pipelineConfiguration };

            try
            {
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading pipeline {pipelineConfiguration.Name} from configuration", ex);
            }

            //Common configurations loading with overriding logic from Default -> Engine -> Pipeline -> Activity
            var commonConfigurationToApply = CommonConfigurations.DefaultCommonConfigurations.Clone()
                                                                                             .Override(CommonConfigurations)
                                                                                             .Override(pipelineConfiguration.CommonConfigurations);

            pipelineDefinition.CommonConfigurations = commonConfigurationToApply;

            return pipelineDefinition;
        }
        
        /// <summary>
        /// Starts a pipeline with the given payload, will start the default pipeline if no pipeline is specified
        /// </summary>
        /// <param name="startPipelinePayload"></param>
        public static void StartPipeline(StartPipeline startPipelinePayload)
        {
            //Get pipeline to start with fallback to default pipeline
            var pipelineDefinitionIdToStart = startPipelinePayload?.PipelineDefinitionIdentifier ?? DefaultPipelineIdentifier;

            if (!pipelineDefinitionIdToStart.HasValue)
            {
                DDLogger.LogError<MainEngine>("No pipeline to start, either pass a valid pipeline definition identifier or configure the default one");
                return;
            }

            //We only start pipelines in their latest version, so no need to check ArchivePipelinePool nor try to load an old configuration
            if (PipelinePool.TryGetValue(pipelineDefinitionIdToStart.Value, out var pipelineDefinition))
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
                    PipelineConfigurationHash = pipelineDefinition.Configuration.Hash,
                    PipelineDefinitionIdentifier = pipelineDefinition.Configuration.Identifier,
                    PipelineInstanceIdentifier = Guid.NewGuid()
                },
                TransactionIdentifier = startPipelinePayload.TransactionIdentifier ?? Guid.NewGuid(), //Should the new one follow some pattern so we know it was generated here? Like starting with 4 zeros (0000c4a3-2cdd-40c5-9227-85d290fbfa28) or something like that?

                InstructionPointer = 0,
                Name = pipelineDefinition.Configuration.Name,
                StartedAt = DateTime.UtcNow,
                PipelineSLAMoment = DateTime.UtcNow.AddSeconds(pipelineDefinition.CommonConfigurations.PipelineSLATimeInSeconds ?? CommonConfigurations.DefaultCommonConfigurations.PipelineSLATimeInSeconds.Value),
                MarshalledExternalData = startPipelinePayload.MarshalledExternalData,

                InstructionInstances = new Dictionary<int, InstructionInstance>()
            };

            //Add to running instances
            RunningInstances.TryAdd(pipelineInstance.Key, pipelineInstance);
            PipelineInstancesLocker.TryAdd(pipelineInstance.Key, new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion));

            //Persist to DB
            DokRouterDAL.SaveOrUpdatePipelineInstance(pipelineInstance);

            //Timelog start of the pipeline
            var startMessage = Timelog.Client.Logger.LogStart(Microsoft.Extensions.Logging.LogLevel.Information, JGTimelogDomainTable._51_Pipeline, pipelineInstance.TransactionIdentifier, pipelineInstance.Key.PipelineInstanceIdentifier, null);
            StartedLogMessages[pipelineInstance.Key.PipelineInstanceIdentifier] = startMessage;

            DDLogger.LogInfo<MainEngine>($"Pipeline {pipelineDefinition.Configuration.Name} ({pipelineDefinition.Configuration.Identifier}) started new instance with identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

            //Trigger start of first activity
            StartActivity(new StartActivityIn()
            {
                PipelineInstanceKey = pipelineInstance.Key
            });
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
                erroredPipeline.FinishedAt = DateTime.UtcNow;
                erroredPipeline.ErroredAt = DateTime.UtcNow;
                erroredPipeline.ErrorMessage = errorMessage;
                DokRouterDAL.ErrorPipelineInstance(erroredPipeline);
                RunningInstances.TryRemove(erroredPipeline.Key, out _);
                PipelineInstancesLocker.TryRemove(erroredPipeline.Key, out _);
                DDLogger.LogError<MainEngine>($"Pipeline instance {erroredPipeline.Key.PipelineInstanceIdentifier} errored with message: {errorMessage}. It was moved to the error collection and removed from running instances");

                //Timelog stop of the pipeline
                if (StartedLogMessages.TryGetValue(erroredPipeline.Key.PipelineInstanceIdentifier, out var startPipelineLogMessage))
                { Timelog.Client.Logger.LogStop(startPipelineLogMessage); }
                else
                { Timelog.Client.Logger.LogStop(Microsoft.Extensions.Logging.LogLevel.Information, JGTimelogDomainTable._51_Pipeline, erroredPipeline.TransactionIdentifier, erroredPipeline.Key.PipelineInstanceIdentifier, null); }
            }
            finally
            {
                locker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Moves a pipeline instance to a finished state, should only be called when the pipeline ends correctly, otherwise call ErrorPipeline
        /// </summary>
        public static void FinishPipeline(PipelineInstance pipelineInstance)
        {
            PipelineInstancesLocker.TryGetValue(pipelineInstance.Key, out var locker);
            if (locker == null)
            {
                //Pipeline not running anymore
                DDLogger.LogWarn<MainEngine>($"Pipeline instance {pipelineInstance.Key} not found in running instances - Request for FinishPipeline will be discarded");
                return;
            }
            locker.EnterWriteLock();
            try
            {
                pipelineInstance.FinishedAt = DateTime.UtcNow;
                DokRouterDAL.FinishPipelineInstance(pipelineInstance);
                RunningInstances.TryRemove(pipelineInstance.Key, out _);
                PipelineInstancesLocker.TryRemove(pipelineInstance.Key, out _);
                DDLogger.LogInfo<MainEngine>($"Pipeline instance {pipelineInstance.Key.PipelineInstanceIdentifier} finished. It was moved to the Finished collection and removed from running instances");

                //Timelog stop of the pipeline
                if (StartedLogMessages.TryGetValue(pipelineInstance.Key.PipelineInstanceIdentifier, out var startPipelineLogMessage))
                { Timelog.Client.Logger.LogStop(startPipelineLogMessage); }
                else
                { Timelog.Client.Logger.LogStop(Microsoft.Extensions.Logging.LogLevel.Information, JGTimelogDomainTable._51_Pipeline, pipelineInstance.TransactionIdentifier, pipelineInstance.Key.PipelineInstanceIdentifier, null); }
            }
            finally
            {
                locker.ExitWriteLock();
            }
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
                //Get Pipeline Definition
                PipelineDefinition pipelineDefinition = null;
                //Check in Pipeline Pool
                if (!PipelinePool.TryGetValue(startActivityPayload.PipelineInstanceKey.PipelineDefinitionIdentifier, out pipelineDefinition))
                {
                    //Check in Archive Pipeline Pool
                    if (ArchivePipelinePool.ContainsKey(startActivityPayload.PipelineInstanceKey.PipelineDefinitionIdentifier))
                    {
                        ArchivePipelinePool[startActivityPayload.PipelineInstanceKey.PipelineDefinitionIdentifier].TryGetValue(startActivityPayload.PipelineInstanceKey.PipelineConfigurationHash, out pipelineDefinition);
                    }
                }

                if (pipelineDefinition == null)
                {
                    DDLogger.LogError<MainEngine>($"No pipeline definition found for identifier: {startActivityPayload.PipelineInstanceKey.PipelineDefinitionIdentifier}");
                    return;
                }

                //Get Pipeline Instance
                if (!RunningInstances.TryGetValue(startActivityPayload.PipelineInstanceKey, out var pipelineInstance))
                {
                    DDLogger.LogError<MainEngine>($"Cannot find running instance: {startActivityPayload.PipelineInstanceKey.PipelineInstanceIdentifier} for pipeline definition: {pipelineDefinition} ({pipelineDefinition.Configuration.Identifier})");
                    return;
                }

                if (pipelineInstance.InstructionPointer >= pipelineDefinition.Configuration.InstructionsConfiguration.Count)
                {
                    DDLogger.LogError<MainEngine>($"Current index overflow, cannot start instruction pointer {pipelineInstance.InstructionPointer} for pipeline {pipelineDefinition.Configuration.Name} ({pipelineDefinition.Configuration.Identifier}) as it only defines {pipelineDefinition.Configuration.InstructionsConfiguration.Count} instructions");
                    ErrorPipeline(pipelineInstance, $"Inconsistent Pipeline: Current index overflow, cannot start instruction pointer {pipelineInstance.InstructionPointer}");
                    return;
                }

                Guid nextActivityIdentifier = Guid.Empty;
                ActivityDefinition activityDefinition = null;

                //Find next activity to be executed
                while (activityDefinition == null)
                {
                    //Obtain next instruction configuration based on pipeline definition and current instruction pointer
                    var nextInstructionConfiguration = pipelineDefinition.Configuration.InstructionsConfiguration[pipelineInstance.InstructionPointer];
                    if(!pipelineInstance.InstructionInstances.ContainsKey(pipelineInstance.InstructionPointer))
                    {
                        //First activity of the instruction, create the instruction instance
                        pipelineInstance.InstructionInstances.Add(pipelineInstance.InstructionPointer, new InstructionInstance()
                        {
                            ActivityInstances = new Dictionary<int, Dictionary<Guid, ActivityInstance>>(),
                            CurrentActivityIndex = 0,
                            CurrentCycleCounter = 0,
                            NumberCycles = Math.Min(EvaluateExpressionToInteger(nextInstructionConfiguration.NumberCyclesExpression, pipelineInstance.InstanceData, 1), 
                                                    EvaluateExpressionToInteger(nextInstructionConfiguration.MaxNumberCyclesExpression, pipelineInstance.InstanceData, EngineDefaulMaxNumberCycles))
                        });
                    }

                    //Obtain the instruction instance
                    var instructionInstance = pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer];
                    switch (nextInstructionConfiguration.Kind)
                    {
                        //TODO: Check execution condition as we might want to prevent an instruction to be executed based on some expression condition

                        //Direct activity execution, just one activity to be executed, so we just need to check if it was already executed
                        case PipelineInstructionKind.Activity:
                        case PipelineInstructionKind.Cycle:
                            var currentActivityIndex = instructionInstance.CurrentActivityIndex;
                            var currentCycleIndex = instructionInstance.CurrentCycleCounter;

                            if (!instructionInstance.ActivityInstances.ContainsKey(currentCycleIndex)) { instructionInstance.ActivityInstances.Add(currentCycleIndex, new Dictionary<Guid, ActivityInstance>()); }

                            while (nextActivityIdentifier == Guid.Empty)
                            {
                                if (currentActivityIndex >= nextInstructionConfiguration.ActivityIdentifiers.Count)
                                {
                                    //Reached the end of the activities, move to next cycle
                                    currentActivityIndex = 0;
                                    currentCycleIndex++;
                                    //Notice that when activity Kind is Activity instructionInstance.NumberCycles shall be 1
                                    if (currentCycleIndex >= instructionInstance.NumberCycles)
                                    {
                                        //Reached the end of the cycles, move to next instruction
                                        break;
                                    }
                                    
                                    if (!instructionInstance.ActivityInstances.ContainsKey(currentCycleIndex)) { instructionInstance.ActivityInstances.Add(currentCycleIndex, new Dictionary<Guid, ActivityInstance>()); }
                                }

                                var currentActivityIdentifier = nextInstructionConfiguration.ActivityIdentifiers[currentActivityIndex];

                                if (!instructionInstance.ActivityInstances[currentCycleIndex].ContainsKey(currentActivityIdentifier))
                                {
                                    //This is the next activity to be executed
                                    nextActivityIdentifier = currentActivityIdentifier;
                                    break;
                                }

                                if (!instructionInstance.ActivityInstances[currentCycleIndex][currentActivityIdentifier].EndedAt.HasValue)
                                {
                                    //This activity did not finish, it will be retried
                                    nextActivityIdentifier = currentActivityIdentifier;
                                    break;
                                }

                                currentActivityIndex++;
                            }

                            instructionInstance.CurrentActivityIndex = currentActivityIndex;
                            instructionInstance.CurrentCycleCounter = currentCycleIndex;
                            break;

                        //case PipelineInstructionKind.GoTo:
                        //    break;
                        default:
                            throw new NotImplementedException($"Instruction kind is unknown or not implemented: {nextInstructionConfiguration.Kind.ToString()}");
                    }

                    if (nextActivityIdentifier == Guid.Empty)
                    {
                        //All activities have been executed, move to next instruction
                        pipelineInstance.InstructionPointer++;
                        DDLogger.LogDebug<MainEngine>($"Moving forward instruction pointer for Pipeline {pipelineInstance.Name} ({pipelineInstance.Key.PipelineDefinitionIdentifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");
                        if (pipelineInstance.InstructionPointer >= pipelineDefinition.Configuration.InstructionsConfiguration.Count)
                        {
                            //Reached the end of the pipeline, pipeline is finished
                            FinishPipeline(pipelineInstance);
                            return;
                        }
                    }
                    else if (!ActivityPool.TryGetValue(nextActivityIdentifier, out activityDefinition))
                    {
                        //Activity definition not found in pool!
                        //Should we only start latest version of activities?
                        //If we want to start older versions, we need to check the ArchiveActivityPool, this means that when the pipeline instance is created,
                        //we need to load the configuration of the pipeline and activities to persist those configuration hashes within the pipeline definition
                        DDLogger.LogWarn<MainEngine>($"Cannot start activity with identifier {nextActivityIdentifier} that would be the next one within pipeline {pipelineInstance.Name} ({pipelineInstance.Key.PipelineDefinitionIdentifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier} - Activity not found in the pool. Will skip this activity and proceed");

                        //Init Activity Instance if first
                        if (!pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].CurrentCycleActivityInstances.ContainsKey(nextActivityIdentifier))
                        {
                            pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].CurrentCycleActivityInstances.Add(nextActivityIdentifier, new ActivityInstance()
                            {
                                ActivityConfigurationHash = "N/A",
                                Name = "N/A",
                                StartedAt = DateTime.UtcNow,
                                EndedAt = DateTime.UtcNow,
                                ActivitySLAMoment = DateTime.UtcNow,
                                IsSuccess = false,
                                ErrorMessage = "Activity not found in the pool, skipping it",
                                Executions = new List<ActivityExecution>()
                                {
                                    new ActivityExecution()
                                    {
                                        Key = new ActivityExecutionKey()
                                        {
                                            PipelineInstanceKey = pipelineInstance.Key,
                                            ActivityConfigurationHash = "N/A",
                                            ActivityDefinitionIdentifier = nextActivityIdentifier,
                                            ActivityExecutionIdentifier = Guid.NewGuid()
                                        },
                                        StartedAt = DateTime.UtcNow,
                                        EndedAt = DateTime.UtcNow,
                                        ActivityTrySLAMoment = DateTime.UtcNow,
                                        ErrorMessage = "Activity not found in the pool, skipping it",
                                        IsSuccess = false
                                    }
                                }
                            });
                        }

                        nextActivityIdentifier = Guid.Empty; //Clear variable so we won't get into an infinite loop upon reaching end of activity list
                    }
                }

                //If we reach here, we should have a next activity to be executed
                DDLogger.LogDebug<MainEngine>($"Starting activity {activityDefinition.Configuration.Name} ({activityDefinition.Configuration.Identifier}) in Pipeline {pipelineInstance.Name} ({pipelineInstance.Key.PipelineDefinitionIdentifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

                //Init Activity Instance if first
                if (!pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].CurrentCycleActivityInstances.ContainsKey(nextActivityIdentifier))
                {
                    pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].CurrentCycleActivityInstances.Add(nextActivityIdentifier, new ActivityInstance()
                    {
                        ActivityConfigurationHash = activityDefinition.Configuration.Hash,
                        Name = activityDefinition.Configuration.Name,
                        StartedAt = DateTime.UtcNow,
                        ActivitySLAMoment = DateTime.UtcNow.AddSeconds(activityDefinition.CommonConfigurations.ActivitySLATimeInSeconds ?? pipelineDefinition.CommonConfigurations.ActivitySLATimeInSeconds ?? CommonConfigurations.DefaultCommonConfigurations.ActivitySLATimeInSeconds.Value),
                        Executions = new List<ActivityExecution>()
                    });
                }

                //Check previous executions of the activity that have not ended - This might need to be revisited if we want parallel activity executions
                if (pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].CurrentCycleActivityInstances[nextActivityIdentifier].Executions.Any())
                {
                    //Not first execution of activity, meaning we are doing some retry, aditional actions are needed

                    //All previous activity executions should be flagged as errored
                    foreach (var activityExecution in pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].CurrentCycleActivityInstances[nextActivityIdentifier].Executions)
                    {
                        if (!activityExecution.EndedAt.HasValue)
                        {
                            activityExecution.EndedAt = DateTime.UtcNow;
                            activityExecution.IsSuccess = false;
                            activityExecution.ErrorMessage = "Another execution started before this one ended";
                        }
                    }

                    //Check if there are any retries available
                    if (pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].CurrentCycleActivityInstances[nextActivityIdentifier].Executions.Count >= activityDefinition.CommonConfigurations.RetryOnSLAExpiredMaxRetries + 1)
                    {
                        //No more executions! Do not start the activity, terminate the pipeline instead
                        DDLogger.LogError<MainEngine>($"activity {activityDefinition.Configuration.Name} ({activityDefinition.Configuration.Identifier}) in Pipeline {pipelineDefinition.Configuration.Name} ({pipelineDefinition.Configuration.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier} reached limit of retries. Pipeline will be errored");
                        ErrorPipeline(pipelineInstance, $"Activity {activityDefinition.Configuration.Name} reached limit of retries");
                        return;
                    }

                    //Check if retry obeys the delay - Review as it is ending the activity in the same cycle thus this is never evaluated to false
                    //so it exits the start activty without persisting the EndedAt and the pipeline instance is not updated
                    //On the next cycle it will keep retrying the same activity
                    //if (DateTime.UtcNow < pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].ActivityInstances[nextActivityIdentifier].Executions.Last().EndedAt.Value.AddSeconds(activityDefinition.CommonConfigurations.RetryOnSLAExpiredDelayInSeconds ?? pipelineDefinition.CommonConfigurations.RetryOnSLAExpiredDelayInSeconds ?? CommonConfigurations.DefaultCommonConfigurations.RetryOnSLAExpiredDelayInSeconds.Value))
                    //{
                    //    //Retry not allowed yet, wait for the delay to pass
                    //    DDLogger.LogDebug<MainEngine>($"Activity {activityDefinition.Configuration.Name} ({activityDefinition.Configuration.Identifier}) in Pipeline {pipelineDefinition.Configuration.Name} ({pipelineDefinition.Configuration.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier} required retry within delay period");
                    //    return;
                    //}
                }

                var activityExecutionKey = new ActivityExecutionKey()
                {
                    PipelineInstanceKey = pipelineInstance.Key,
                    ActivityConfigurationHash = activityDefinition.Configuration.Hash,
                    ActivityDefinitionIdentifier = activityDefinition.Configuration.Identifier,
                    ActivityExecutionIdentifier = Guid.NewGuid(),
                    CycleCounter = pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].CurrentCycleCounter,
                };

                pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].CurrentCycleActivityInstances[nextActivityIdentifier].Executions.Add(new ActivityExecution()
                {
                    Key = activityExecutionKey,
                    StartedAt = DateTime.UtcNow,
                    ActivityTrySLAMoment = DateTime.UtcNow.AddSeconds(activityDefinition.CommonConfigurations.ActivityTrySLATimeInSeconds ?? CommonConfigurations.DefaultCommonConfigurations.ActivityTrySLATimeInSeconds.Value),
                });

                //Run the activity Async
                Task.Run(() =>
                {
                    ActivityStarter.OnStartActivity(activityDefinition, new StartActivityOut()
                    {
                        ActivityExecutionKey = activityExecutionKey,
                        MarshalledExternalData = pipelineInstance.MarshalledExternalData,
                    });
                });

                DDLogger.LogInfo<MainEngine>($"Started activity {activityDefinition.Configuration.Name} ({activityDefinition.Configuration.Identifier}) in Pipeline {pipelineDefinition.Configuration.Name} ({pipelineDefinition.Configuration.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

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
            //Switch between end a trigger activity or a regular activity
            if(endActivityPayload.ActivityExecutionKey.PipelineTriggerIdentifier.HasValue)
            {
                EngineTriggering.OnPreConditionActivityEnd(endActivityPayload);
                return;
            }

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
                //Get pipeline definition from pool with fallback for archive and for repository
                PipelineDefinition pipelineDefinition = null;
                PipelinePool.TryGetValue(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineDefinitionIdentifier, out pipelineDefinition);
                if(pipelineDefinition == null || pipelineDefinition.Configuration.Hash != endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineConfigurationHash)
                {
                    //Check in current memory archive
                    if(ArchivePipelinePool.TryGetValue(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineDefinitionIdentifier, out var pipelineArchivedConfigurations))
                    {
                        pipelineArchivedConfigurations.TryGetValue(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineConfigurationHash, out pipelineDefinition);
                    }

                    if (pipelineDefinition == null)
                    {
                        //Check in DB
                        var pipelineConfiguration = DokRouterDAL.GetArchivePipelineConfigurationByHash(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineConfigurationHash);
                        if(pipelineConfiguration != null)
                        {
                            //Add it to memory archive for future usage
                            pipelineDefinition = BuildPipelineDefinitionFromConfiguration(pipelineConfiguration);
                            if(!ArchivePipelinePool.ContainsKey(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineDefinitionIdentifier))
                            {
                                ArchivePipelinePool.Add(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineDefinitionIdentifier, new Dictionary<string, PipelineDefinition>());
                            }
                            ArchivePipelinePool[endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineDefinitionIdentifier][endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineConfigurationHash] = pipelineDefinition;
                        }
                    }
                }

                if (pipelineDefinition == null)
                {
                    DDLogger.LogError<MainEngine>($"No pipeline definition found for identifier: {endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineDefinitionIdentifier} neither in pool nor in archive with configuration hash: {endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineConfigurationHash}");
                    return;
                }

                if (!RunningInstances.TryGetValue(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey, out var pipelineInstance))
                {
                    DDLogger.LogError<MainEngine>($"Cannot find running instance: {endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineInstanceIdentifier} for pipeline definition: {pipelineDefinition.Configuration.Name} ({pipelineDefinition.Configuration.Identifier})");
                    return;
                }

                //Get activity definition from pool with fallback for archive and for repository - mainly for debugging and logging purposes
                ActivityDefinition activityDefinition = null;
                ActivityPool.TryGetValue(endActivityPayload.ActivityExecutionKey.ActivityDefinitionIdentifier, out activityDefinition);
                if (activityDefinition == null || activityDefinition.Configuration.Hash != endActivityPayload.ActivityExecutionKey.ActivityConfigurationHash)
                {
                    //Check in current memory archive
                    if (ArchiveActivityPool.TryGetValue(endActivityPayload.ActivityExecutionKey.ActivityDefinitionIdentifier, out var activityArchivedConfigurations))
                    {
                        activityArchivedConfigurations.TryGetValue(endActivityPayload.ActivityExecutionKey.ActivityConfigurationHash, out activityDefinition);
                    }

                    if (activityDefinition == null)
                    {
                        //Check in DB
                        var activityConfiguration = DokRouterDAL.GetArchiveActivityConfigurationByHash(endActivityPayload.ActivityExecutionKey.ActivityConfigurationHash);
                        if (activityConfiguration != null)
                        {
                            //Add it to memory archive for future usage
                            activityDefinition = BuildActivityDefinitionFromConfiguration(activityConfiguration);
                            if (!ArchiveActivityPool.ContainsKey(endActivityPayload.ActivityExecutionKey.ActivityDefinitionIdentifier))
                            {
                                ArchiveActivityPool.Add(endActivityPayload.ActivityExecutionKey.ActivityDefinitionIdentifier, new Dictionary<string, ActivityDefinition>());
                            }
                            ArchiveActivityPool[endActivityPayload.ActivityExecutionKey.ActivityDefinitionIdentifier][endActivityPayload.ActivityExecutionKey.ActivityConfigurationHash] = activityDefinition;
                        }
                    }
                }

                if (activityDefinition == null)
                {
                    DDLogger.LogError<MainEngine>($"No activity definition found for identifier: {endActivityPayload.ActivityExecutionKey.ActivityDefinitionIdentifier} neither in pool nor in archive with configuration hash: {endActivityPayload.ActivityExecutionKey.ActivityConfigurationHash}");
                    return;
                }

                var activityExecutionKey = endActivityPayload.ActivityExecutionKey;
                if (pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].CurrentCycleActivityInstances.ContainsKey(activityExecutionKey.ActivityDefinitionIdentifier))
                {
                    var activityInstance = pipelineInstance.InstructionInstances[pipelineInstance.InstructionPointer].CurrentCycleActivityInstances[activityExecutionKey.ActivityDefinitionIdentifier];
                    var activityExecution = activityInstance.Executions.FirstOrDefault(e => e.Key.Equals(activityExecutionKey));

                    if (activityExecution == null)
                    {
                        DDLogger.LogError<MainEngine>($"Execution {activityExecutionKey.ActivityExecutionIdentifier} for activity {activityDefinition.Configuration.Name} ({activityDefinition.Configuration.Identifier}) in Pipeline {pipelineDefinition.Configuration.Name} ({pipelineDefinition.Configuration.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier} attempted to finish but was not found! Nothing will be done.");
                        return;
                    }

                    if (activityExecution.EndedAt.HasValue)
                    {
                        DDLogger.LogWarn<MainEngine>($"Execution {activityExecutionKey.ActivityExecutionIdentifier} for activity {activityDefinition.Configuration.Name} ({activityDefinition.Configuration.Identifier}) in Pipeline {pipelineDefinition.Configuration.Name} ({pipelineDefinition.Configuration.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier} attempted to finish but already ended! Nothing will be done.");
                        return;
                    }

                    if (activityInstance.EndedAt.HasValue)
                    {
                        DDLogger.LogWarn<MainEngine>($"Activity {activityDefinition.Configuration.Name} ({activityDefinition.Configuration.Identifier}) in Pipeline {pipelineDefinition.Configuration.Name} ({pipelineDefinition.Configuration.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier} attempted to finish but already ended! Nothing will be done.");
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
                if(pipelineInstance.InstanceData == null) { pipelineInstance.InstanceData = new Dictionary<string, string>(); }
                if(endActivityPayload.ProcessInstanceData != null)
                {
                    foreach(var key in endActivityPayload.ProcessInstanceData.Keys)
                    {
                        pipelineInstance.InstanceData[key] = endActivityPayload.ProcessInstanceData[key];
                    }
                }

                //Persist to DB - Finished activity
                DokRouterDAL.SaveOrUpdatePipelineInstance(pipelineInstance);

                //Timelog stop of the activity
                if (StartedLogMessages.TryGetValue(activityExecutionKey.ActivityExecutionIdentifier, out var startMessage))
                { Timelog.Client.Logger.LogStop(startMessage); }
                else
                { Timelog.Client.Logger.LogStop(Microsoft.Extensions.Logging.LogLevel.Information, JGTimelogDomainTable._51_Activity, pipelineInstance.TransactionIdentifier, activityExecutionKey.ActivityExecutionIdentifier, null); }

                DDLogger.LogInfo<MainEngine>($"Ended activity {activityDefinition.Configuration.Name} ({activityDefinition.Configuration.Identifier}) in Pipeline {pipelineDefinition.Configuration.Name} ({pipelineDefinition.Configuration.Identifier}) with instance identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

                //Start next activity
                StartActivity(new StartActivityIn()
                {
                    PipelineInstanceKey = endActivityPayload.ActivityExecutionKey.PipelineInstanceKey
                });
            }
            finally
            {
                locker.ExitWriteLock();
            }
        }

        /// <summary>
        /// This will evaluate a pipeline expression and return the int value that corresponds to the parsed value
        /// TODO: This can be moved to some common expression evaluator or metaprogramming module
        /// </summary>
        /// <returns></returns>
        public static int EvaluateExpressionToInteger(string initialExpression, Dictionary<string, string> supportData, int defaultValue)
        {
            if (String.IsNullOrEmpty(initialExpression)) { return defaultValue; }

            //Replace all {keys} with the respective value from the support data dictionary
            string expression = initialExpression;
            while (expression.Contains("{") && expression.Contains("}"))
            {
                var keyStart = expression.IndexOf("{");
                var keyEnd = expression.IndexOf("}");
                if(keyStart < keyEnd)
                {
                    var key = expression.Substring(keyStart + 1, keyEnd - keyStart - 1);
                    if(supportData.ContainsKey(key))
                    {
                        expression = expression.Replace($"{{{key}}}", supportData[key]);
                    }
                    else
                    {
                        expression = expression.Replace($"{{{key}}}", "");
                    }
                }
            }

            if (int.TryParse(expression, out var result)) { DDLogger.LogDebug<MainEngine>($"Evaluate Expression '{expression}' from initial expression '{initialExpression}' will return: '{result}'"); return result; }

            DDLogger.LogDebug<MainEngine>($"Evaluate Expression '{expression}' from initial expression '{initialExpression}' will return default value of: '{default}'");
            return defaultValue;
        }
    }
}
