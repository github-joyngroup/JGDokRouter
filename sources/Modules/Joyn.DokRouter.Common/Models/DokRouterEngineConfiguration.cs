namespace Joyn.DokRouter.Common.Models
{
    /// <summary>
    /// Represents a configuration for the DokRouter Engine. This configuration will be used to start the engine and define the pipelines and activities that will be executed.
    /// </summary>
    public class DokRouterEngineConfiguration
    {
        /// <summary>
        /// Unique hash for this configuration. This hash will be used to identify the configuration and its version.
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// The pipeline that will be used as default when no pipeline is specified.
        /// </summary>
        public Guid? DefaultPipelineIdentifier { get; set; }

        /// <summary>
        /// Assembly that contains the class that contains the method that will be executed when an activity is to be started
        /// </summary>
        public string OnStartActivityAssembly { get; set; }

        /// <summary>
        /// Class that contains the method that will be executed when an activity is to be started
        /// </summary>
        public string OnStartActivityClass { get; set; }

        /// <summary>
        /// Method that will be executed when an activity is to be started
        /// </summary>
        public string OnStartActivityMethod { get; set; }

        /// <summary>
        /// The configuration for the pipelines that will be executed by the engine
        /// </summary>
        public List<EDDokRouterEngineConfiguration_Pipelines> Pipelines { get; set; }

        /// <summary>
        /// Common configurations that will be used by the engine, might be overriden by the pipeline and activity configurations
        /// If not specified, the default configurations will be used
        /// </summary>
        public CommonConfigurations CommonConfigurations { get; set; }
    }

    /// <summary>
    /// Configures a single pipeline that will be executed by the engine
    /// </summary>
    public class EDDokRouterEngineConfiguration_Pipelines
    {
        /// <summary>
        /// Unique hash for this configuration. This hash will be used to identify the pipeline and its version.
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// The name of the Pipeline, used mainly for debugging and logging purposes
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Unique identifier of the pipeline
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// Activities that will be executed by the pipeline
        /// </summary>
        public List<EDDokRouterEngineConfiguration_Activities> Activities { get; set; }

        /// <summary>
        /// Common configurations that will be used by the pipeline, might be overriden by the activity configurations
        /// </summary>
        public CommonConfigurations CommonConfigurations { get; set; }
    }

    /// <summary>
    /// Configuration of a single activity that will be executed by the engine
    /// </summary>
    public class EDDokRouterEngineConfiguration_Activities
    {
        /// <summary>
        /// Unique hash for this configuration. This hash will be used to identify the pipeline and its version.
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// The name of the activity, used mainly for debugging and logging purposes
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Unique identifier of the activity
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// The order number of the activity in the pipeline
        /// </summary>
        public int OrderNumber { get; set; }

        /// <summary>
        /// Whether the activity is disabled or not
        /// </summary>
        public bool Disabled { get; set; }

        /// <summary>
        /// Kind of activity to execute
        /// </summary>
        public ActivityKind Kind { get; set; }

        /// <summary>
        /// For Kind = Direct, the assembly that contains the class that contains the method that will be executed when the activity is to be executed
        /// </summary>
        public string DirectActivityAssembly { get; set; }

        /// <summary>
        /// For Kind = Direct, the class that contains the method that will be executed when the activity is to be executed
        /// </summary>
        public string DirectActivityClass { get; set; }

        /// <summary>
        /// For Kind = Direct, the method that will be executed when the activity is to be executed
        /// </summary>
        public string DirectActivityMethod { get; set; }

        /// <summary>
        /// For Kind = HTTP, the URL that will be used to execute the activity
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// For Kind == KafkaEvent, the Kafka topic that will be used to execute the activity
        /// </summary>
        public string KafkaTopic { get; set; }

        /// <summary>
        /// Common configurations that will be used by the activity
        /// </summary>
        public CommonConfigurations CommonConfigurations { get; set; }
    }
}