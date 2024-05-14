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
            return GenericMongoDAL<LLMProcessData, LLMProcessDataRunningMapper>.GetObjectById(id);
        }
        
        public static void SaveOrUpdate(LLMProcessData llmProcessData)
        {
            GenericMongoDAL<LLMProcessData, LLMProcessDataRunningMapper>.UpdateObject(llmProcessData);
        }
    }

    public class BaseMongoMapper
    {
        public static string ConnectionString { get; set; }
        public static string DatabaseName { get; set; }

        public static void Startup(string connectionString, string databaseName)
        {
            ConnectionString = connectionString;
            DatabaseName = databaseName;

            //So guids are readable in Mongo the same way they are in C#
            //BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

            //Epocas, 27/03/2020 > this allows POCO classes to have less fields than those existing in the Mongo database
            var pack = new MongoDB.Bson.Serialization.Conventions.ConventionPack();
            pack.Add(new MongoDB.Bson.Serialization.Conventions.IgnoreExtraElementsConvention(true));
            MongoDB.Bson.Serialization.Conventions.ConventionRegistry.Register("My Solution Conventions", pack, t => true);
        }
    }

    public class LLMProcessDataRunningMapper : BaseMongoMapper
    {
        public static string CollectionName => "LLMProcessData";
        public static bool UseTransactions => false;
    }
}
