namespace Joyn.LLMDriver.Models
{
    public class UploadedFileInformation
    {
        //Generated or extracted from the uploaded file
        public Guid EnvelopeUuid { get; set; }
        public string OriginalFileName { get; set; }
        public string OriginalContentType { get; set; }
        public string LocalFilePath { get; set; }

        //Produced by Generate Metadata Activity
        /// <summary>Infered by the extensions, can differ from the upload</summary>
        /// TODO: We shall match the content type with the magic byte as we do in DDV2
        public string ContentType { get; set; }

        public int TotalPages { get; set; }

        public List<string> PageImagesPaths { get; set; }
    }
}
