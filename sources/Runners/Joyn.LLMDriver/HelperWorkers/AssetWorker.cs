using Joyn.LLMDriver.DAL;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Text;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class AssetWorker
    {
        public static void PutAsset(string baseAssetsFilePath, string fileName, byte[] fileContent, LLMProcessData llmProcessData, string assetKey, bool bUpdateDb)
        {
            //Persist new asset
            string assetPath = Path.Combine($"{baseAssetsFilePath}", fileName);
            File.WriteAllBytes(assetPath, fileContent);

            //Save the updated LLMProcessData object
            Dictionary<string, string> assetInformationData = llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.AssetInformationKey) ? BsonSerializer.Deserialize<Dictionary<string, string>>(llmProcessData.ProcessData[LLMProcessDataConstants.AssetInformationKey]) : new Dictionary<string, string>();

            assetInformationData[assetKey] = assetPath;
            llmProcessData.ProcessData[LLMProcessDataConstants.AssetInformationKey] = assetInformationData.ToBsonDocument();

            if (bUpdateDb)
            {
                LLMProcessDataDAL.SaveOrUpdate(llmProcessData);
            }
        }

        public static void PutAssetText(string baseAssetsFilePath, string fileName, string fileContent, LLMProcessData llmProcessData, string assetKey, bool bUpdateDb)
        {
            PutAsset(baseAssetsFilePath, fileName, Encoding.UTF8.GetBytes(fileContent), llmProcessData, assetKey, bUpdateDb);
        }

        public static byte[] GetAssetBinary(string baseAssetsFilePath, string assetKey, LLMProcessData llmProcessData)
        {
            //Obtain the asset information
            Dictionary<string, string> assetInformationData = llmProcessData.ProcessData.ContainsKey(LLMProcessDataConstants.AssetInformationKey) ? BsonSerializer.Deserialize<Dictionary<string, string>>(llmProcessData.ProcessData[LLMProcessDataConstants.AssetInformationKey]) : new Dictionary<string, string>();

            //Obtain the asset path
            string assetPath = assetInformationData.ContainsKey(assetKey) ? assetInformationData[assetKey] : null;

            if(string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            {
                return null;
            }

            //Obtain the asset content
            byte[] assetContent = File.ReadAllBytes(assetPath);

            return assetContent;
        }

        public static string GetAssetText(string baseAssetsFilePath, string assetKey, LLMProcessData llmProcessData)
        {
            return Encoding.UTF8.GetString(GetAssetBinary(baseAssetsFilePath, assetKey, llmProcessData));
        }
    }
}
