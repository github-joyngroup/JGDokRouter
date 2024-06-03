using DocDigitizer.Common.DAL;
using DocDigitizer.Common.DAL.SimpleMongo;

namespace Joyn.LLMDriver.DAL
{
    public class LLMProcessDataConstants
    {
        //Constants to access the ProcessData dictionary
        public const string ActivityModelKey = "ActivityModel";

        public const string StartPayloadKey = "StartPayload";
        public const string StartPayload_DomainIdKey = "DomainId";

        public const string FileInformationKey = "FileInformation";
        public const string FileInformation_OriginalFileNameKey = "OriginalFileName";
        public const string FileInformation_OriginalContentType = "OriginalContentType";
        public const string FileInformation_LocalFilePath = "LocalFilePath";
        public const string FileInformation_ContentTypeKey = "ContentType";

        public const string AssetInformationKey = "AssetInformation";
        public const string AssetKeyPageImage = "PageImage";
        public const string AssetKeyPageStructure = "PageStructure";
        public const string AssetKeyPageDDOCRMap = "PageDDOCRMap";
        public const string AssetKeyPageFullText = "PageFullText";
        public const string AssetKeyPageTextLines = "PageTextLines";
        public const string AssetKeyConsolidatedTextLines = "ConsolidatedTextLines";

        public const string AssetKeyChatGPTClassify = "ChatGPTClassify";
        public const string AssetKeyChatGPTExtract = "ChatGPTExtract";
        public const string AssetKeyClassificationError = "ClassificationError";

        public const string LLMDocumentExtractionKey = "LLMDocumentExtraction";

        public const string CandidateInformationKey = "CandidateInformation";

        /*
        public const string MetadataKey = "Metadata";
        public const string EnvelopeUuidKey = "EnvelopeUuid";
        public const string OriginalFileNameKey = "OriginalFileName";
        public const string ContentTypeKey = "ContentType";
        public const string TotalPagesKey = "TotalPages";

        public const string OriginalFilePathKey = "OriginalFilePath";
        
        


        private const string ActivityModelKey = "ActivityModel";
        
        private const string DomainIdKey = "DomainId";
        */
    }
}