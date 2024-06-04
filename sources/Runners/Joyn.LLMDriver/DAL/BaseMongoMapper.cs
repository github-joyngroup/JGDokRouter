using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;

namespace Joyn.LLMDriver.DAL
{
    public class BaseMongoMapper
    {
        public static string ConnectionString { get; set; }
        public static string DatabaseName { get; set; }

        public static void Startup(string connectionString, string databaseName)
        {
            ConnectionString = connectionString;
            DatabaseName = databaseName;

            //So guids are readable in Mongo the same way they are in C#
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonDefaults.GuidRepresentation = GuidRepresentation.Standard;
            BsonSerializer.RegisterSerializer(typeof(DateTime), new IsoDateTimeSerializer());

            //Epocas, 27/03/2020 > this allows POCO classes to have less fields than those existing in the Mongo database
            var pack = new MongoDB.Bson.Serialization.Conventions.ConventionPack();
            pack.Add(new MongoDB.Bson.Serialization.Conventions.IgnoreExtraElementsConvention(true));
            MongoDB.Bson.Serialization.Conventions.ConventionRegistry.Register("My Solution Conventions", pack, t => true);
        }
    }

    public class IsoDateTimeSerializer : SerializerBase<DateTime>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime value)
        {
            var isoDateString = value.ToString("o"); // ISO 8601 format
            context.Writer.WriteString(isoDateString);
        }

        public override DateTime Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.CurrentBsonType == BsonType.DateTime) 
            {
                return new DateTime(context.Reader.ReadDateTime());
            }
            else
            {
                var isoDateString = context.Reader.ReadString();
                return DateTime.Parse(isoDateString, null, System.Globalization.DateTimeStyles.RoundtripKind);
            }
        }
    }
}
