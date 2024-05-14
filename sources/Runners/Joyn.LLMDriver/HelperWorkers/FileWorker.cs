using DocDigitizer.Common.Extensions;
using DocDigitizer.Common.Logging;
using Joyn.LLMDriver.DAL;
using Joyn.LLMDriver.Models;
using Joyn.LLMDriver.PSAspects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using NReco.PdfRenderer;
using System;
using System.Text.Json;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class FileWorker
    {
        //Static variables
        private static NReco.PdfRenderer.PdfToImageConverter converter = new NReco.PdfRenderer.PdfToImageConverter();
        private static NReco.PdfRenderer.PdfInfo nRecoInfo = new NReco.PdfRenderer.PdfInfo();

        public static void Startup(string nRecoLicenceOwner, string nRecoLicenceKey)
        {
            //NReco
            NReco.PdfRenderer.License.SetLicenseKey(nRecoLicenceOwner, nRecoLicenceKey);

            //TODO: Move to configuration
            converter.Dpi = 150;
            converter.EnableAntiAliasing = false;
            converter.EnableVectorAntiAliasing = false;
            converter.ScaleTo = 1920;
        }

        #region Produce Metadata

        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._50_ProduceMetadata)]
        public static void ProduceMetadata(ActivityModel model, Guid executionId)
        {
            //Obtain the LLMProcessData object
            var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
            if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.FileInformationKey))
            {
                //No File was uploaded - Do nothing as this step is not needed
                return;
            }

            //var fileInformation = JsonSerializer.Deserialize<UploadedFileInformation>(llmProcessData.ProcessData[LLMProcessDataConstants.FileInformationKey]);
            var fileInformation = BsonSerializer.Deserialize<UploadedFileInformation>(llmProcessData.ProcessData[LLMProcessDataConstants.FileInformationKey]);

            //Generate the metadata and save it to the ProcessData dictionary
            fileInformation.ContentType = InferContentTypeFromExtension(Path.GetExtension(fileInformation.LocalFilePath));
            fileInformation.TotalPages = GetTotalPages(fileInformation.LocalFilePath);

            //Save the updated LLMProcessData object
            llmProcessData.ProcessData[LLMProcessDataConstants.FileInformationKey] = fileInformation.ToBsonDocument();
            LLMProcessDataDAL.SaveOrUpdate(llmProcessData);
        }

        public static string InferContentTypeFromExtension(string extension)
        {
            switch (extension)
            {
                case ".pdf":
                    return "application/pdf";

                case ".jpeg":
                case ".jpg":
                    return "image/jpeg";

                case ".png":
                    return "image/png";

                case ".tiff":
                    return "image/tiff";

                case ".txt":
                case ".log":
                    return "text/plain";

                case ".json":
                    return "application/json";

                case ".html":
                    return "text/html";

                default:
                    throw new Exception($"Unknown content type for extension: {extension}");
            }
        }

        public static int GetTotalPages(string filePath)
        {
            int totalPages = 0;

            try
            {
                var pdfBytes = File.ReadAllBytes(filePath);
                PdfInfo.PdfInformation info = null;

                using (MemoryStream pdfStream = new MemoryStream(pdfBytes))
                {
                    info = nRecoInfo.GetPdfInfo(pdfStream);
                    totalPages = info.Pages;
                }
            }
            catch (Exception ex)
            {
                DDLogger.LogException<FileWorker>($"Error GettingPdfInfo for filePath: {filePath}", ex);
            }

            return totalPages;
        }

        #endregion
    }
}
