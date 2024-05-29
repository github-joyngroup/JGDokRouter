using DocDigitizer.Common.DAL;
using DocDigitizer.Common.DAL.SimpleMongo;
using Joyn.DokRouter.Common.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization;

namespace Joyn.LLMDriver.DAL
{
    public class LLMProcessData : IUniqueIdentifier
    {
        public string Id { get; set; }
        public int DefaultRankOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        
        public Dictionary<string, BsonDocument> ProcessData { get; set; }
    }

    public static class LLMProcessDataDAL
    {
        public static LLMProcessData Get(string id)
        {
            return GenericMongoDAL<LLMProcessData, LLMProcessDataMapper>.GetObjectById(id);
        }
        
        public static void SaveOrUpdate(LLMProcessData llmProcessData)
        {
            GenericMongoDAL<LLMProcessData, LLMProcessDataMapper>.UpdateObject(llmProcessData);
        }
    }
    
    public class LLMProcessDataMapper : BaseMongoMapper
    {
        public static string CollectionName => "LLMProcessData";
        public static bool UseTransactions => false;
    }
}
