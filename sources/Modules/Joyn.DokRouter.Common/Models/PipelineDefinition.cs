
namespace Joyn.DokRouter.Common.Models
{
    /// <summary>
    /// Definition of a pipeline that can be executed by the engine
    /// </summary>
    public class PipelineDefinition
    {
        /// <summary>
        /// Unique Identifier of the Pipeline Definition
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// Name of the Pipeline Definition, used mainly for logging and debugging
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// List of activities that are part of the pipeline
        /// </summary>
        public List<PipelineActivityDefinition> Activities { get; set; }

        /// <summary>
        /// Common configurations that should be applied to this pipeline activities, might override those on the engine level
        /// </summary>
        public CommonConfigurations CommonConfigurations { get; set; }
    }

    /// <summary>
    /// Definition of an activity that is part of a pipeline
    /// </summary>
    public class PipelineActivityDefinition
    {
        /// <summary>
        /// Unique Identifier of the Activity Definition
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// Name of the Activity Definition, used mainly for logging and debugging
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Order in which the activity should be executed within the pipeline
        /// </summary>
        public int OrderNumber { get; set; }
        
        /// <summary>
        /// Definition of the execution of the activity - How it should be executed when triggered by the engine
        /// </summary>
        public ActivityExecutionDefinition ExecutionDefinition { get; set; }

        /// <summary>
        /// Common configurations that should be applied to this activity, might override those on the pipeline or engine level
        /// </summary>
        public CommonConfigurations CommonConfigurations { get; set; }    
    }

    /// <summary>
    /// Definition of how a specific activity should be executed by the engine
    /// </summary>
    public class ActivityExecutionDefinition
    {
        /// <summary>
        /// Kind of activity execution, determines how the activity should be executed
        /// Direct - The activity is executed directly by the engine
        /// HTTP - Execution of the activity relies on an HTTP request
        /// KafkaEvent - Execution of the activity relies on pushing a Kafka message
        /// </summary>
        public ActivityKind Kind { get; set; }

        /// <summary>
        /// For direct executions, the handler that should be called when the activity is executed
        /// </summary>
        public OnExecuteActivityHandler DirectActivityHandler { get; set; }

        /// <summary>
        /// For HTTP executions, the URL that should be called when the activity is executed
        /// </summary>
        public string Url { get; set; }
        
        /// <summary>
        /// For kafka event executions, the topic that should be used to push the message
        /// </summary>
        public string KafkaTopic { get; set; }
    }

    /// <summary>
    /// The kind of activities that can be executed by the engine
    /// </summary>
    public enum ActivityKind
    {
        /// The activity is executed directly by the engine
        Direct = 10,
        
        /// Execution of the activity relies on an HTTP request
        HTTP = 20,

        /// Execution of the activity relies on pushing a Kafka message
        KafkaEvent = 30
    }
}
