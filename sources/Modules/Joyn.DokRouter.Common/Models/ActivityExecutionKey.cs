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
        /// The identifier of the activity definition that this activity execution is based on.
        /// </summary>
        public Guid ActivityDefinitionIdentifier { get; set; }

        /// <summary>
        /// The identifier of the activity execution.
        /// </summary>
        public Guid ActivityExecutionIdentifier { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is ActivityExecutionKey key)
            {
                return key.PipelineInstanceKey.Equals(this.PipelineInstanceKey) &&
                       key.ActivityDefinitionIdentifier == this.ActivityDefinitionIdentifier &&
                       key.ActivityExecutionIdentifier == this.ActivityExecutionIdentifier;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PipelineInstanceKey.GetHashCode(), ActivityDefinitionIdentifier, ActivityExecutionIdentifier);
        }

        public override string ToString()
        {
            //Most effective way to turn the 4 guids into a key

            byte[] bytes = new byte[64]; // 16 bytes per GUID x 4
            PipelineInstanceKey.PipelineDefinitionIdentifier.ToByteArray().CopyTo(bytes, 0);
            PipelineInstanceKey.PipelineInstanceIdentifier.ToByteArray().CopyTo(bytes, 16);
            ActivityDefinitionIdentifier.ToByteArray().CopyTo(bytes, 32);
            ActivityExecutionIdentifier.ToByteArray().CopyTo(bytes, 48);

            return $"{PipelineInstanceKey.ConfigurationHash}|{Convert.ToBase64String(bytes)}";
        }

        public static ActivityExecutionKey FromString(string key)
        {
            var parts = key.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2) { throw new ArgumentException("Invalid key format"); }
            var configurationHash = parts[0];
            byte[] bytes = Convert.FromBase64String(parts[1]);

            return new ActivityExecutionKey
            {
                PipelineInstanceKey = new PipelineInstanceKey
                {
                    ConfigurationHash = configurationHash,
                    PipelineDefinitionIdentifier = new Guid(bytes.Skip(0).Take(16).ToArray()),
                    PipelineInstanceIdentifier = new Guid(bytes.Skip(16).Take(16).ToArray())
                },
                ActivityDefinitionIdentifier = new Guid(bytes.Skip(32).Take(16).ToArray()),
                ActivityExecutionIdentifier = new Guid(bytes.Skip(48).Take(16).ToArray())
            };
        }
    }
}
