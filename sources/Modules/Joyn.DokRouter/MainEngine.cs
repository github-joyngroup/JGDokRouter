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

        internal static DokRouterEngineConfiguration DokRouterEngineConfiguration { get; private set; }
        internal static Dictionary<Guid, PipelineDefinition> PipelineDefinitions { get; private set; } = new Dictionary<Guid, PipelineDefinition>();

        public static void Startup(DokRouterEngineConfiguration configuration)
        {
            lock (dokRouterEngineConfigurationLocker)
            {
                DokRouterEngineConfiguration = configuration;

                foreach (var pipelineConfiguration in configuration.Pipelines)
                {
                    PipelineDefinitions.Add(pipelineConfiguration.Identifier, BuildPipelineFromConfiguration(pipelineConfiguration));
                }

                //TODO: Log loaded pipeline definitions - what is loaded and with what configurations

                //TODO: Load from DB running instances
            }
        }

        private static PipelineDefinition BuildPipelineFromConfiguration(EDDokRouterEngineConfiguration_Pipelines pipelineConfiguration)
        {
            return new PipelineDefinition()
            {
                Name = pipelineConfiguration.Name,
                Identifier = pipelineConfiguration.Identifier,
                Activities = pipelineConfiguration.Activities.Select(a => BuildActivityFromConfiguration(a)).OrderBy(a => a.OrderNumber).ToList()
            };
        }

        private static PipelineActivityDefinition BuildActivityFromConfiguration(EDDokRouterEngineConfiguration_Activities activityConfiguration)
        {
            var onExecuteAssembly = Assembly.Load(activityConfiguration.OnStartActivityAssembly);
            if (onExecuteAssembly == null) { throw new Exception($"OnStartActivityAssembly assembly '{activityConfiguration.OnStartActivityAssembly}' not found"); }
            var OnExecuteClass = onExecuteAssembly.GetType(activityConfiguration.OnStartActivityClass);
            if (OnExecuteClass == null) { throw new Exception($"OnStartActivityClass type '{activityConfiguration.OnStartActivityClass}' in assembly '{activityConfiguration.OnStartActivityAssembly}' not found"); }
            var onMessageMethod = OnExecuteClass.GetMethod(activityConfiguration.OnStartActivityMethod);
            if (onMessageMethod == null) { throw new Exception($"OnStartActivityMethod method '{activityConfiguration.OnStartActivityMethod}' in class '{activityConfiguration.OnStartActivityClass}' not found"); }

            OnStartActivityHandler onStartActivity = (OnStartActivityHandler) Delegate.CreateDelegate(typeof(OnStartActivityHandler), onMessageMethod);

            return new PipelineActivityDefinition()
            {
                Name = activityConfiguration.Name,
                Identifier = activityConfiguration.Identifier,
                OrderNumber = activityConfiguration.OrderNumber,
                OnStartActivity = onStartActivity
            };
        }


        public static void OnStartPipeline(Joyn.DokRouter.Payloads.StartPipeline startPipelinePayload)
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
                StartPipeline(pipelineDefinition, startPipelinePayload);
            }
            else
            {
                DDLogger.LogError<MainEngine>($"Undefined or not configured pipeline to start: {pipelineDefinitionIdToStart}");
                return;
            }
        }

        private static ConcurrentDictionary<PipelineInstanceKey, PipelineInstance> RunningInstances = new ConcurrentDictionary<PipelineInstanceKey, PipelineInstance>();

        private static void StartPipeline(PipelineDefinition pipelineDefinition, StartPipeline startPipelinePayload)
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
                ExternalData = startPipelinePayload.ExternalData,

                ActivityExecutions = new Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>>()
            };

            //Add to running instances
            RunningInstances.TryAdd(pipelineInstance.Key, pipelineInstance);

            //TO DO: PERSIST TO DB

            DDLogger.LogInfo<MainEngine>($"Pipeline {pipelineDefinition.Name} ({pipelineDefinition.Identifier}) started new instance with identifier {pipelineInstance.Key.PipelineInstanceIdentifier}");

            //Trigger start of first activity
            OnStartActivity(new Joyn.DokRouter.Payloads.StartActivity()
            {
                PipelineInstanceKey = pipelineInstance.Key
            });
        }

        
        public static void OnStartActivity(Joyn.DokRouter.Payloads.StartActivity startActivityPayload)
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

            activityDefinition.OnStartActivity(activityExecutionKey, pipelineInstance.ExternalData);

            //TO DO: PERSIST TO DB
        }

        public static void OnEndActivity(Joyn.DokRouter.Payloads.EndActivity endActivityPayload)
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
                OnStartActivity(new Joyn.DokRouter.Payloads.StartActivity()
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
