using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.Common.Models
{
    /// <summary>
    /// Represents a single activity configuration that is available in the activity pool to be used in the pipelines
    /// </summary>
    public class ActivityConfiguration
    {
        /// <summary>
        /// Unique hash for this activity configuration. This hash will be used to identify the configuration and its version.
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// Friendly name of the activity, used mainly for debugging and logging purposes
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Helper description of the activity, used mainly for debugging and logging purposes
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Unique identifier of the activity, used to reference the activity in the pipeline definitions
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// Whether or not the activity is disabled and should not be used in the pipelines. Disabled activities at configuration level will not be added to the activity pool
        /// If a pipeline references an activity that is disabled, the pipeline will skip the activity and continue with the next one
        /// </summary>
        public bool Disabled { get; set; }

        /// <summary>
        /// Common configurations that will be used by the activity
        /// </summary>
        public CommonConfigurations CommonConfigurations { get; set; }

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
        KafkaEvent = 30,
    }
}
