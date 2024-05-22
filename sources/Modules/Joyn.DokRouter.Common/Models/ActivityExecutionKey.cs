namespace Joyn.DokRouter.Common.Models
{
    /// <summary>
    /// Identifies an activity being executed by providing the identifier of the corresponding pipeline and the pair of keys that uniquely identifies the activity execution.
    /// </summary>
    public class ActivityExecutionKey
    {
        /// <summary>
        /// The key that identifies the pipeline instance that this activity execution belongs to.
        /// </summary>
        public PipelineInstanceKey PipelineInstanceKey { get; set; }

        /// <summary>
        /// The configuration hash of the activity that ended
        /// </summary>
        public string ActivityConfigurationHash { get; set; }

        /// <summary>
        /// The identifier of the activity definition that this activity execution is based on.
        /// </summary>
        public Guid ActivityDefinitionIdentifier { get; set; }

        /// <summary>
        /// The identifier of the activity execution.
        /// </summary>
        public Guid ActivityExecutionIdentifier { get; set; }

        /// <summary>
        /// When the activity is executed within a cycle, this number indicates the ordinal within the cycle execution
        /// </summary>
        public int? CycleCounter { get; set; }

        /// <summary>
        /// When the activity is executed by a trigger, this will identify the trigger instace that started the activity
        /// </summary>
        public Guid? PipelineTriggerIdentifier { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is ActivityExecutionKey key)
            {
                return key.PipelineInstanceKey.Equals(this.PipelineInstanceKey) &&
                       key.ActivityConfigurationHash == this.ActivityConfigurationHash &&
                       key.ActivityDefinitionIdentifier == this.ActivityDefinitionIdentifier &&
                       key.ActivityExecutionIdentifier == this.ActivityExecutionIdentifier &&
                       key.PipelineTriggerIdentifier == this.PipelineTriggerIdentifier;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PipelineInstanceKey.GetHashCode(), ActivityConfigurationHash, ActivityDefinitionIdentifier, ActivityExecutionIdentifier, PipelineTriggerIdentifier);
        }
    }
}
