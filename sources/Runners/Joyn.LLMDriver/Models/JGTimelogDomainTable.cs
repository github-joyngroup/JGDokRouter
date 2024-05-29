namespace Joyn.LLMDriver.Models
{
    public class JGTimelogDomainTable
    {
        //50 for FileWorker
        public const uint _50_FileWorker = 0x32000000; //50.0.0.0
        public const uint _50_ProduceMetadata = 0x32010000; //50.1.0.0
        public const uint _50_ProduceImages = 0x32020000; //50.2.0.0
        public const uint _50_ProduceOCRAssets = 0x32030000; //50.3.0.0
        public const uint _50_ConsolidateAssets = 0x32040000; //50.4.0.0
        public const uint _50_ClassifyUsingChatGPT = 0x32050000; //50.5.0.0
        public const uint _50_PerformExtractionsUsingChatGPT = 0x32060000; //50.6.0.0

        //60 for ResumatorWorker
        public const uint _60_ResumatorWorker = 0x3C000000; //60.0.0.0
        public const uint _60_UpdateJobs = 0x3C010000; //60.1.0.0
        public const uint _60_UpdateCandidates = 0x3C020000; //60.2.0.0

        //70 for LLMWorker
        public const uint _70_LLMWorker = 0x46000000; //70.0.0.0
        public const uint _70_ClassifyUsingLLM = 0x46010000; //70.1.0.0
        public const uint _70_PerformLLMExtraction = 0x46020000; //70.2.0.0
        
        //80 for DocDigitizerWorker
        public const uint _80_DocDigitizerWorker = 0x50000000; //80.0.0.0
        public const uint _80_ProduceWorldObject = 0x50010000; //80.1.0.0
        
    }
}
