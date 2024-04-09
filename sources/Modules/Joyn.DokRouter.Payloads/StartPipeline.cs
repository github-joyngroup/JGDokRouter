namespace Joyn.DokRouter.Payloads
{
    public class StartPipeline
    {
        public Guid? PipelineDefinitionIdentifier { get; set; }

        public string SerializedExternalData { get; set; }
    }
}
