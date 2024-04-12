namespace Joyn.DokRouter.Common.Payloads
{
    /// <summary>
    /// Payload used to start a pipeline
    /// </summary>
    public class StartPipeline
    {
        /// <summary>
        /// The pipeline definition identifier
        /// </summary>
        public Guid? PipelineDefinitionIdentifier { get; set; }

        /// <summary>
        /// External data serialized, will be passed to the activities when they are started and updated when the activities end,
        /// The pipeline engine will never look or modify this data, as it is up to the activities to handle it
        /// </summary>
        public byte[] MarshalledExternalData { get; set; }
    }
}
