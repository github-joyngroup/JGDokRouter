namespace Joyn.LLMDriver.Models
{
    public class JGTimelogDomainTable
    {
        //DDLLMClonePipeline 50 for first octet
        public const uint _50_DDLLMClonePipeline = 0x32000000; //50.0.0.0
        public const uint _50_ProduceMetadata = 0x32010000; //50.1.0.0
        public const uint _50_ProduceImages = 0x32020000; //50.2.0.0
        public const uint _50_ProduceOCRAssets = 0x32030000; //50.3.0.0
        public const uint _50_ConsolidateAssets = 0x32040000; //50.4.0.0
        public const uint _50_ClassifyUsingChatGPT = 0x32050000; //50.5.0.0
        public const uint _50_PerformExtractionsUsingChatGPT = 0x32060000; //50.6.0.0
    }
}
