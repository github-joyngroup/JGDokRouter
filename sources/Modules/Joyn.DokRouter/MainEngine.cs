using DocDigitizer.Common.Logging;
using Joyn.DokRouter.Models;
using Joyn.DokRouter.Payloads;
using System.Collections.Concurrent;
using System.Reflection;

namespace Joyn.DokRouter
{
    public class MainEngine
    {
        private static readonly object dokRouterEngineConfigurationLocker = new object();

        private static DokRouterEngineConfiguration DokRouterEngineConfiguration { get; set; }
        private static Dictionary<Guid, PipelineDefinition> PipelineDefinitions { get; set; } = new Dictionary<Guid, PipelineDefinition>();

        private static OnStartActivityHandler OnStartActivity;

        public static void Startup(DokRouterEngineConfiguration configuration)
        {
            lock (dokRouterEngineConfigurationLocker)
            {
                DokRouterEngineConfiguration = configuration;

                var onExecuteAssembly = Assembly.Load(configuration.OnStartActivityAssembly);
                if (onExecuteAssembly == null) { throw new Exception($"OnStartActivityAssembly assembly '{configuration.OnStartActivityAssembly}' not found"); }
                var OnExecuteClass = onExecuteAssembly.GetType(configuration.OnStartActivityClass);
                if (OnExecuteClass == null) { throw new Exception($"OnStartActivityClass type '{configuration.OnStartActivityClass}' in assembly '{configuration.OnStartActivityAssembly}' not found"); }
                var onMessageMethod = OnExecuteClass.GetMethod(configuration.OnStartActivityMethod);
                if (onMessageMethod == null) { throw new Exception($"OnStartActivityMethod method '{configuration.OnStartActivityMethod}' in class '{configuration.OnStartActivityClass}' not found"); }

                try
                {
                    OnStartActivity = (OnStartActivityHandler)Delegate.CreateDelegate(typeof(OnStartActivityHandler), onMessageMethod);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Unable to load Main OnStartActivityHandler configured with method '{configuration.OnStartActivityMethod}' in class '{configuration.OnStartActivityClass}'", ex);
                }

                foreach (var pipelineConfiguration in configuration.Pipelines)
                {
                    PipelineDefinitions.Add(pipelineConfiguration.Identifier, BuildPipelineFromConfiguration(pipelineConfiguration));
                }

                //TODO: Log loaded pipeline definitions - what is loaded and with what configurations

                //TODO: Launch TEST pipeline for each pipeline definition

                //TODO: Load from DB running instances
            }
        }

        private static PipelineDefinition BuildPipelineFromConfiguration(EDDokRouterEngineConfiguration_Pipelines pipelineConfiguration)
        {
            return new PipelineDefinition()
            {
                Name = pipelineConfiguration.Name,
                Identifier = pipelineConfiguration.Identifier,
                Activities = pipelineConfiguration.Activities.FindAll(a => !a.Disabled).Select(a => BuildActivityFromConfiguration(a)).OrderBy(a => a.OrderNumber).ToList()
            };
        }

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


        public static void StartPipeline(Joyn.DokRouter.Payloads.StartPipeline startPipelinePayload)
        {
            //Get pipeline to start with fallback to default pipeline
            var pipelineDefinitionIdToStart = startPipelinePayload?.PipelineDefinitionIdentifier ?? DokRouterEngineConfiguration.DefaultPipelineIdentifier;
            
            if(!pipelineDefinitionIdToStart.HasValue) 
            {
                DDLogger.LogError<MainEngine>("No pipeline to start, either pass a valid pipeline definition identifier or configure the default one");
                return;
            }

            if (PipelineDefinitions.TryGetValue(pipelineDefinitionIdToStart.Value, out var pipelineDefinition))
            {
                InnerStartPipeline(pipelineDefinition, startPipelinePayload);
            }
            else
            {
                DDLogger.LogError<MainEngine>($"Undefined or not configured pipeline to start: {pipelineDefinitionIdToStart}");
                return;
            }
        }

        private static ConcurrentDictionary<PipelineInstanceKey, PipelineInstance> RunningInstances = new ConcurrentDictionary<PipelineInstanceKey, PipelineInstance>();

        private static void InnerStartPipeline(PipelineDefinition pipelineDefinition, StartPipeline startPipelinePayload)
        {
            //Create new instance
            var pipelineInstance = new PipelineInstance()
            {
                Key = new PipelineInstanceKey()
                {
                    PipelineDefinitionIdentifier = pipelineDefinition.Identifier,
                    PipelineInstanceIdentifier = Guid.NewGuid()
                },
                CurrentActivityIndex = 0,
                StartedAt = DateTime.UtcNow,
                SerializedExternalData = startPipelinePayload.SerializedExternalData,

                ActivityExecutions = new Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>>()
            };

            //Add to running instances
            RunningInstances.TryAdd(pipelineInstance.Key, pipelineInstance);

            //TO DO: PERSIST TO DB

            DDLogger.LogInfo<MainEngine>($"Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) started new instance with identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

            //Trigger start of first activity
            StartActivity(new Joyn.DokRouter.Payloads.StartActivityIn()
            {
                PipelineInstanceKey = pipelineInstance.Key
            });
        }

        
        public static void StartActivity(Joyn.DokRouter.Payloads.StartActivityIn startActivityPayload)
        {
            if(!PipelineDefinitions.TryGetValue(startActivityPayload.PipelineInstanceKey.PipelineDefinitionIdentifier, out var pipelineDefinition))
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
                OnStartActivity(activityDefinition.ExecutionDefinition, new StartActivityOut()
                {
                    ActivityExecutionKey = activityExecutionKey,
                    SerializedExternalData = pipelineInstance.SerializedExternalData
                });
            });

            //TO DO: PERSIST TO DB
        }

        public static void EndActivity(Joyn.DokRouter.Payloads.EndActivity endActivityPayload)
        {
            if (!PipelineDefinitions.TryGetValue(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineDefinitionIdentifier, out var pipelineDefinition))
            {
                DDLogger.LogError<MainEngine>($"No pipeline definition found for identifier: {endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineDefinitionIdentifier}");
                return;
            }

            if (!RunningInstances.TryGetValue(endActivityPayload.ActivityExecutionKey.PipelineInstanceKey, out var pipelineInstance))
            {
                DDLogger.LogError<MainEngine>($"Cannot find running instance: {endActivityPayload.ActivityExecutionKey.PipelineInstanceKey.PipelineInstanceIdentifier} for pipeline definition: {pipelineDefinition} ({pipelineDefinition.Identifier})");
                return;
            }

            if(pipelineInstance.ActivityExecutions.ContainsKey(pipelineInstance.CurrentActivityIndex) && pipelineInstance.ActivityExecutions[pipelineInstance.CurrentActivityIndex].ContainsKey(endActivityPayload.ActivityExecutionKey))
            {
                pipelineInstance.ActivityExecutions[pipelineInstance.CurrentActivityIndex][endActivityPayload.ActivityExecutionKey].EndedAt = DateTime.UtcNow;
                pipelineInstance.ActivityExecutions[pipelineInstance.CurrentActivityIndex][endActivityPayload.ActivityExecutionKey].IsSuccess = endActivityPayload.IsSuccess;
                pipelineInstance.ActivityExecutions[pipelineInstance.CurrentActivityIndex][endActivityPayload.ActivityExecutionKey].ErrorMessage = endActivityPayload.ErrorMessage;
            }

            //Move to next activity
            pipelineInstance.CurrentActivityIndex++;

            //TO DO: PERSIST TO DB - Finished activity

            //Check if there are activities available
            if (pipelineInstance.CurrentActivityIndex < pipelineDefinition.Activities.Count)
            {
                var activityDefinition = pipelineDefinition.Activities[pipelineInstance.CurrentActivityIndex];
                StartActivity(new Joyn.DokRouter.Payloads.StartActivityIn()
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

                //TO DO: PERSIST TO DB - Finished pipeline
            }
        }

        public static List<StateData> GetState()
        {
            Dictionary<Guid, Dictionary<Guid, PipelineInstance>> tempDic = new Dictionary<Guid, Dictionary<Guid, PipelineInstance>>();

            foreach (var runningInstance in RunningInstances)
            {
                if(!tempDic.ContainsKey(runningInstance.Key.PipelineDefinitionIdentifier))
                {
                    tempDic.Add(runningInstance.Key.PipelineDefinitionIdentifier, new Dictionary<Guid, PipelineInstance>());
                }

                tempDic[runningInstance.Key.PipelineDefinitionIdentifier].Add(runningInstance.Key.PipelineInstanceIdentifier, runningInstance.Value);
            }

            return tempDic.Select(rI => new StateData()
            {
                PipelineDefinitionIdentifier = PipelineDefinitions[rI.Key].Name,
                PipelineInstances = rI.Value.Select(pi => pi.Value).ToList()
            }).ToList();
        }
        
    }

    public class StateData
    { 
        public string PipelineDefinitionIdentifier { get; set; }
        public List<PipelineInstance> PipelineInstances { get; set; }
    }
}
