using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.Common.Models
{
    /// <summary>
    /// Represents a pipeline configuration that is available to be used
    /// </summary>
    public class PipelineConfiguration
    {
        /// <summary>
        /// Unique hash for this configuration. This hash will be used to identify the configuration and its version.
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// Friendly name of the pipeline, used mainly for debugging and logging purposes
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Helper description of the pipeline, used mainly for debugging and logging purposes
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Unique identifier of the pipeline
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// Whether or not the pipeline is disabled and should not be used. Disabled pipelines at configuration level will not be available to be used in the engine
        /// </summary>
        public bool Disabled { get; set; }

        /// <summary>
        /// For automation purposes, the pipeline can have a trigger configuration, that will be used to start the pipeline execution
        /// </summary>
        public PipelineTriggerConfiguration Trigger { get; set; }

        /// <summary>
        /// Common configurations that will be used by the activity
        /// </summary>
        public CommonConfigurations CommonConfigurations { get; set; }

        public List<PipelineInstructionsConfiguration> InstructionsConfiguration { get; set; }
    }
}
