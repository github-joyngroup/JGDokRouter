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