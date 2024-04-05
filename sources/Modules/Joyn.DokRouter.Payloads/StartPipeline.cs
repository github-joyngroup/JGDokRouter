namespace Joyn.DokRouter.Payloads
{
    public class StartPipeline
    {
        public Guid? PipelineDefinitionIdentifier { get; set; }

        public object ExternalData { get; set; }
    }
}
