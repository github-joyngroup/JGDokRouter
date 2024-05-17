using Joyn.DokRouter.Common.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using NHibernate.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.MongoDAL
{
    public class MainStorageHelper
    {
        public static void Startup(string connectionString, string databaseName)
        {
            BsonSerializer.RegisterSerializer<Dictionary<int, InstructionInstance>>(new InstructionInstanceFromIntDictionarySerializer());
            BsonSerializer.RegisterSerializer<Dictionary<Guid, ActivityInstance>>(new ActivityInstancesFromGuidDictionarySerializer());
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));


            //Epocas, 27/03/2020 > this allows POCO classes to have less fields than those existing in the Mongo database
            var pack = new MongoDB.Bson.Serialization.Conventions.ConventionPack();
            pack.Add(new MongoDB.Bson.Serialization.Conventions.IgnoreExtraElementsConvention(true));
            MongoDB.Bson.Serialization.Conventions.ConventionRegistry.Register("My Solution Conventions", pack, t => true);

            BaseMongoMapper.Startup(connectionString, databaseName);
        }
    }
}
