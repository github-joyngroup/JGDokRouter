using DocDigitizer.Common.DataStructures.Cartesian;
using DocDigitizer.Common.DataStructures.OCR;
using DocDigitizer.Common.Exceptions;
using DocDigitizer.Common.Extensions;
using DocDigitizer.Common.Logging;
using Google.Cloud.Vision.V1;
using Joyn.LLMDriver.DAL;
using Joyn.LLMDriver.Models;
using Joyn.LLMDriver.PSAspects;
using Joyn.Timelog.Common.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using NReco.PdfRenderer;
using System;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class FileWorker
    {
        private static readonly string AllTextLinesPageDelimiter = "/***DD***\r\n";

        //Static variables
        private static NReco.PdfRenderer.PdfToImageConverter converter = new NReco.PdfRenderer.PdfToImageConverter();
        private static NReco.PdfRenderer.PdfInfo nRecoInfo = new NReco.PdfRenderer.PdfInfo();
        private static ImageAnnotatorClient googleVisionClient = ImageAnnotatorClient.Create();

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

        /// <summary>
        /// Produces the metadata of the uploaded file within a process
        /// If no file was uploaded, this step is skipped
        /// Metadata produced in this step includes the content type, based on the file extension and the total number of pages in the file
        /// Information is expected to exist within LLMProcessData.ProcessData[LLMProcessDataConstants.FileInformationKey], thats also where the updated information is saved
        /// </summary>
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

        #region Produce Images

        /// <summary>
        /// Produces the images of the uploaded file within a process
        /// If no file was uploaded, this step is skipped
        /// Information is expected to exist within LLMProcessData.ProcessData[LLMProcessDataConstants.FileInformationKey], 
        /// Produced images are stored as assets in the base assets folder
        /// </summary>
        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._50_ProduceImages)]
        public static void ProduceImages(ActivityModel model, Guid executionId)
        {
            try
            {
                //Obtain the LLMProcessData object
                var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
                if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.FileInformationKey))
                {
                    //No File was uploaded - Do nothing as this step is not needed
                    return;
                }
                var fileInformation = BsonSerializer.Deserialize<UploadedFileInformation>(llmProcessData.ProcessData[LLMProcessDataConstants.FileInformationKey]);
                if(fileInformation.TotalPages == 0)
                {
                    DDLogger.LogWarn<FileWorker>($"{executionId} - ProduceImages - Total Pages is 0, cannot generate images for file: {fileInformation.OriginalFileName}");
                    return;
                }

                Dictionary<string, string> assetInformationData = new Dictionary<string, string>();
                if(llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.AssetInformationKey))
                {
                    assetInformationData = BsonSerializer.Deserialize<Dictionary<string, string>>(llmProcessData.ProcessData[LLMProcessDataConstants.AssetInformationKey]);
                }

                string filePath = fileInformation.LocalFilePath;
                string fileName = Path.GetFileName(filePath);

                var pdfBytes = File.ReadAllBytes(filePath);

                List<string> pageImagesPaths = new List<string>(new string[fileInformation.TotalPages]);

                Parallel.ForEach(Enumerable.Range(0, fileInformation.TotalPages), pageIdx =>
                {
                    using (MemoryStream outMs = new MemoryStream())
                    {
                        converter.GenerateImage(filePath, (pageIdx + 1), NReco.PdfRenderer.ImageFormat.Jpeg, outMs);
                        AssetWorker.PutAsset(model.BaseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyPageImage}_{(pageIdx + 1)}.jpg", outMs.ToArray(), llmProcessData, $"{LLMProcessDataConstants.AssetKeyPageImage}_{pageIdx + 1}", bUpdateDb: false);
                    }
                });
                
                //Save the updated LLMProcessData object
                LLMProcessDataDAL.SaveOrUpdate(llmProcessData);
            }
            catch (Exception ex)
            {
                if (ex.Message == "Command Line Error: Incorrect password")
                {
                    throw new PDFPasswordException("Incorrect Password", ex);
                }
                else if (ex.Message == "Syntax Error: Couldn't read xref table")
                {
                    throw new PDFCorruptedException("Corrupted PDF", ex);
                }
                else
                {
                    DDLogger.LogException<FileWorker>($"{executionId} - ProduceImages - Exception producing images: {ex.Message}", ex);
                    throw;
                }
            }
        }

        #endregion

        #region 03 - Produce OCR Assets

        /// <summary>
        /// Produces the OCR extraction and processed information from the images of the uploaded file within a process
        /// </summary>
        /// <param name="model"></param>
        /// <param name="executionId"></param>
        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._50_ProduceOCRAssets)]
        public static void ProduceOCRAssets(ActivityModel model, Guid executionId)
        {
            try
            {
                //Obtain the LLMProcessData object
                var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
                if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.FileInformationKey))
                {
                    //No File was uploaded - Do nothing as this step is not needed
                    return;
                }
                var fileInformation = BsonSerializer.Deserialize<UploadedFileInformation>(llmProcessData.ProcessData[LLMProcessDataConstants.FileInformationKey]);
                if (fileInformation.TotalPages == 0)
                {
                    DDLogger.LogWarn<FileWorker>($"{executionId} - ProduceOCRAssets - Total Pages is 0, no OCR Assets to produce");
                    return;
                }

                Dictionary<string, string> assetInformationData = new Dictionary<string, string>();
                if (llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.AssetInformationKey))
                {
                    assetInformationData = BsonSerializer.Deserialize<Dictionary<string, string>>(llmProcessData.ProcessData[LLMProcessDataConstants.AssetInformationKey]);
                }

                Parallel.ForEach(assetInformationData.Keys.Where(k => k.StartsWith(LLMProcessDataConstants.AssetKeyPageImage)), (key) =>
                {
                    var pageIdx = int.Parse(key.Split('_').Last()) - 1;
                    ProduceOCRAssetsForImage(assetInformationData[key], model.BaseAssetsFilePath, pageIdx, llmProcessData);
                });

                //Save the updated LLMProcessData object
                //llmProcessData.ProcessData[LLMProcessDataConstants.AssetInformationKey] = assetInformationData.ToBsonDocument();
                LLMProcessDataDAL.SaveOrUpdate(llmProcessData);
            }
            catch(Exception ex)
            {
                DDLogger.LogException<FileWorker>($"{executionId} - ProduceImages - Exception producing OCR Assets: {ex.Message}", ex);
                throw;
            }
        }

        private static void ProduceOCRAssetsForImage(string imageFilePath, string baseAssetsFilePath, int pageIdx, LLMProcessData llmProcessData)
        {
            try
            {
                ConcurrentDictionary<string, string> producedAssetMap = new ConcurrentDictionary<string, string>();

                var imageBytes = File.ReadAllBytes(imageFilePath);
                var image = Google.Cloud.Vision.V1.Image.FromBytes(imageBytes);

                AnnotateImageRequest request = new AnnotateImageRequest
                {
                    Image = image,
                    Features =
                    {
                        new Feature { Type = Feature.Types.Type.DocumentTextDetection },
                        new Feature { Type = Feature.Types.Type.TextDetection }
                    }
                };

                AnnotateImageResponse response = googleVisionClient.Annotate(request);

                //TODO: Check how to replace Newtonsoft with System.Text.Json
                var visionAPIJson = Newtonsoft.Json.Linq.JObject.FromObject(response);
                var ddOCRMap = ParseToDDOCRMap(visionAPIJson);
                var textLines = DocDigitizer.Common.Algorithms.OCRMapFunctions.ExtractLines.ExtractTextLinesByOrientation(ddOCRMap);
                var textLinesAssetContent = string.Join("\r\n", textLines.Lines.ToArray());

                var taskList = new List<Task>
                {
                    Task.Run(() => 
                    {
                        AssetWorker.PutAssetText(baseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyPageStructure}_{(pageIdx + 1)}.json", System.Text.Json.JsonSerializer.Serialize(visionAPIJson), llmProcessData, $"{LLMProcessDataConstants.AssetKeyPageStructure}_{(pageIdx + 1)}", bUpdateDb: false);

                    }),
                    Task.Run(() => 
                    {
                        AssetWorker.PutAssetText(baseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyPageFullText}_{(pageIdx + 1)}.txt", ((response.FullTextAnnotation != null && response.FullTextAnnotation.Text != null) ? response.FullTextAnnotation.Text : ""), llmProcessData, $"{LLMProcessDataConstants.AssetKeyPageFullText}_{(pageIdx + 1)}", bUpdateDb: false );
                    }),
                    Task.Run(() => 
                    {
                        AssetWorker.PutAssetText(baseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyPageDDOCRMap}_{(pageIdx + 1)}.json", System.Text.Json.JsonSerializer.Serialize(ddOCRMap), llmProcessData, $"{LLMProcessDataConstants.AssetKeyPageDDOCRMap}_{(pageIdx + 1)}", bUpdateDb: false );
                    }),
                    Task.Run(() => 
                    {
                        AssetWorker.PutAssetText(baseAssetsFilePath, $"{LLMProcessDataConstants.AssetKeyPageTextLines}_{(pageIdx + 1)}.txt", (!String.IsNullOrWhiteSpace(textLinesAssetContent) ? textLinesAssetContent : ""), llmProcessData, $"{LLMProcessDataConstants.AssetKeyPageTextLines}_{(pageIdx + 1)}", bUpdateDb: false );
                    })
                };
                Task.WaitAll(taskList.ToArray(), CancellationToken.None);
            }
            catch (Exception ex)
            {
                DDLogger.LogException<FileWorker>($"Error ProduceOCRAssetsForImage for imageFilePath: {imageFilePath}", ex);
                throw;
            }
        }

        private static DDOCRMap ParseToDDOCRMap(Newtonsoft.Json.Linq.JObject json)
        {
            if ((json == null) || (json.Root == null) || (json["TextAnnotations"] == null && json["textAnnotations"] == null))
            {
                return null;
            };

            GoogleTags tagsToUse = json["TextAnnotations"] != null ? GoogleTags.PascalTags : GoogleTags.CamelTags;
            var textAnnotationsElm = json[tagsToUse.TextAnnotations];

            DDOCRMap retDDOCRMap = new DDOCRMap()
            {
                FullText = string.Empty,
                Words = new List<DDOCRMapWord>()
            };

            //Protection against invalid textAnnotation (Images with no text)
            if (!textAnnotationsElm.Any()) { return retDDOCRMap; }

            //First element contains the complete bounding box and full text
            var firstElm = textAnnotationsElm[0];

            var pageBoundingBox = GetBoundingBox(firstElm, tagsToUse);

            retDDOCRMap.FullText = firstElm[tagsToUse.Description].ToString();
            retDDOCRMap.MinX = pageBoundingBox.MinX;
            retDDOCRMap.MaxX = pageBoundingBox.MaxX;
            retDDOCRMap.MinY = pageBoundingBox.MinY;
            retDDOCRMap.MaxY = pageBoundingBox.MaxY;
            retDDOCRMap.Words = new List<DDOCRMapWord>();

            for (var idx = 1; idx < textAnnotationsElm.Count(); idx++)
            {
                var myWordElm = textAnnotationsElm[idx];
                retDDOCRMap.Words.Add(new DDOCRMapWord()
                {
                    Word = myWordElm[tagsToUse.Description].ToString(),
                    TokenIndexInOCRMap = idx - 1,
                    Box = GetBoundingBox(myWordElm, tagsToUse),
                }); ;
            }

            DocDigitizer.Common.Algorithms.OCRMapFunctions.TextOrientationCalculator.StampOrientations(retDDOCRMap.Words);
            return retDDOCRMap;
        }

        private static PixelBox GetBoundingBox(Newtonsoft.Json.Linq.JToken elem, GoogleTags tagsToUse)
        {
            //Will create a square around bounding poly from google vision extraction

            var minX = (elem[tagsToUse.BoundingPoly][tagsToUse.Vertices] as Newtonsoft.Json.Linq.JArray).Min((point) => (int)point[tagsToUse.X]);
            var maxX = (elem[tagsToUse.BoundingPoly][tagsToUse.Vertices] as Newtonsoft.Json.Linq.JArray).Max((point) => (int)point[tagsToUse.X]);
            var minY = (elem[tagsToUse.BoundingPoly][tagsToUse.Vertices] as Newtonsoft.Json.Linq.JArray).Min((point) => (int)point[tagsToUse.Y]);
            var maxY = (elem[tagsToUse.BoundingPoly][tagsToUse.Vertices] as Newtonsoft.Json.Linq.JArray).Max((point) => (int)point[tagsToUse.Y]);

            return new PixelBox(new PixelPoint(minX, minY), new PixelPoint(maxX, minY), new PixelPoint(maxX, maxY), new PixelPoint(minX, maxY));
        }

        #endregion


        #region 04 - Consolidate Assets

        /// <summary>
        /// When received file has more than one page, this activity will consolidate the assets of each page into a single asset
        /// </summary>
        /// <param name="model"></param>
        /// <param name="executionId"></param>
        [JGTimelogClientAspect(ModelParameterIndex = 0, ExecutionIdParameterIndex = 1, ExpectedModelType = JGLogClientKnownModelTypes.ActivityModel, Domain = JGTimelogDomainTable._50_ConsolidateAssets)]
        public static void ConsolidateAssets(ActivityModel model, Guid executionId)
        {
            try
            {
                //Obtain the LLMProcessData object
                var llmProcessData = LLMProcessDataDAL.Get(model.DatabaseIdentifier);
                if (llmProcessData == null || llmProcessData.ProcessData == null || !llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.FileInformationKey))
                {
                    //No File was uploaded - Do nothing as this step is not needed
                    return;
                }

                Dictionary<string, string> assetInformationData = new Dictionary<string, string>();
                if (llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.AssetInformationKey))
                {
                    assetInformationData = BsonSerializer.Deserialize<Dictionary<string, string>>(llmProcessData.ProcessData[LLMProcessDataConstants.AssetInformationKey]);
                }

                var textLinesAssetsPaths = new List<string>();
                foreach(var key in assetInformationData.Keys.OrderBy(k => k)) //Order by will assure that the keys are ordered by page number
                {
                    if(key.Contains(LLMProcessDataConstants.AssetKeyPageTextLines))
                    {
                        textLinesAssetsPaths.Add(assetInformationData[key]);
                    }
                }

                if(!textLinesAssetsPaths.Any())
                {
                    DDLogger.LogWarn<FileWorker>($"{executionId} - ConsolidateAssets - Expected AssetInformation to be filled with text lines paths. No Consolidation will be produced.");
                    return;
                }

                //EPocas, 21/05/2024: As of today we only need to consolidate text lines
                //If in the future we need to consolidate other assets, we will need to add them to the consolidation process in this method
                //Current v2 code consolidates lite structures, but that is not needed (for now) for v3

                string baseAssetsFilePath = model.BaseAssetsFilePath;

                //This reads all text lines from each page into memory - this might be a problem for large files
                //If so we should consider changing this to a streaming approach where we open a stream to the destination file and write each page content to it
                //However that would not be parallelizable, so we stick with this approach as we are not expecting large files
                string[] pagesContents = new string[textLinesAssetsPaths.Count];
                Parallel.ForEach(Enumerable.Range(0, textLinesAssetsPaths.Count), pageIdx =>
                {
                    pagesContents[pageIdx] = File.ReadAllText(textLinesAssetsPaths[pageIdx]);
                });

                //Produce new asset
                var allPagesTextLines = string.Join(AllTextLinesPageDelimiter, pagesContents);

                //Persist new asset
                string assetPath = Path.Combine($"{baseAssetsFilePath}", $"{LLMProcessDataConstants.AssetKeyConsolidatedTextLines}.txt");
                File.WriteAllText(assetPath, allPagesTextLines);

                //Save the updated LLMProcessData object
                assetInformationData[LLMProcessDataConstants.AssetKeyConsolidatedTextLines] = assetPath;
                llmProcessData.ProcessData[LLMProcessDataConstants.AssetInformationKey] = assetInformationData.ToBsonDocument();
                LLMProcessDataDAL.SaveOrUpdate(llmProcessData);
            }
            catch (Exception ex)
            {
                DDLogger.LogException<FileWorker>($"{executionId} - ProduceImages - Exception producing OCR Assets: {ex.Message}", ex);
                throw;
            }
        }

        #endregion
    }
}
