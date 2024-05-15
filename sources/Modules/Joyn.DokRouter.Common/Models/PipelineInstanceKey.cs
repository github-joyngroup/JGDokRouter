namespace Joyn.DokRouter.Common.Models
{
    /// <summary>
    /// Identifies a running pipeline instance by providing the pair of keys that uniquely identifies a pipeline instance.
    /// </summary>
    public class PipelineInstanceKey
    {
        /// <summary>
        /// The configuration hash used to create this pipeline instance.
        /// </summary>
        public string ConfigurationHash { get; set; }

        /// <summary>
        /// The identifier of the pipeline definition that this instance is based on.
        /// </summary>
        public Guid PipelineDefinitionIdentifier { get; set; }

        /// <summary>
        /// The identifier of the pipeline instance.
        /// </summary>
        public Guid PipelineInstanceIdentifier { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is PipelineInstanceKey key)
            {
                return key.ConfigurationHash == this.ConfigurationHash &&
                       key.PipelineDefinitionIdentifier == this.PipelineDefinitionIdentifier &&
                       key.PipelineInstanceIdentifier == this.PipelineInstanceIdentifier;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ConfigurationHash, PipelineDefinitionIdentifier, PipelineInstanceIdentifier);
        }
    }
}
