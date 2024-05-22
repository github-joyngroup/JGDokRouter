using System.Text.Json;

namespace Joyn.DokRouter.Common.Models
{
    /// <summary>
    /// Represents a pipeline triger instance that can start a pipeline instance when some condition is met
    /// </summary>
    public class PipelineTriggerInstance
    {
        /// <summary>
        /// The identifier of the pipeline trigger
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// The identifier of the configuration that produced this trigger instance
        /// </summary>
        public Guid ConfigurationIdentifier { get; set; }

        /// <summary>
        /// The pipeline this trigger is associated with
        /// </summary>
        public Guid PipelineIdentifier { get; set; }

        /// <summary>
        /// Activity to execute to decide wether or not the pipeline is to be started
        /// </summary>
        public ActivityDefinition PreConditionActivity { get; set; }

        /// <summary>
        /// Field to look for the boolean value within ProcessInstanceData that will state wether or not to start the pipeline
        /// </summary>
        public string ExpectedPreConditionField { get; set; }


        /// <summary>
        /// When the trigger was last executed
        /// </summary>
        public DateTime? LastExecution { get; set; }

        /// <summary>
        /// When we want the trigger to run again
        /// </summary>
        public DateTime? NextExecution { get; set; }


        /// <summary>Kind of trigger to execute</summary>
        public PipelineTriggerKind Kind { get; set; }

        /// <summary>For Kind = TimerFrequency, the pretended frequency for the timer</summary>
        public int? TimeFrequencySeconds { get; set; }


    }
}
