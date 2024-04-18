using Joyn.DokRouter.Common.Models;
using MongoDB.Bson.IO;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.MongoDAL
{
    public class ActivityInstancesDoubleDictionarySerializer : IBsonSerializer<Dictionary<int, Dictionary<Guid, ActivityInstance>>>
    {
        public Type ValueType => typeof(Dictionary<int, Dictionary<Guid, ActivityInstance>>);

        // Type-specific Serialize method
        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Dictionary<int, Dictionary<Guid, ActivityInstance>> value)
        {
            var writer = context.Writer;
            writer.WriteStartDocument();
            foreach(var outerKvp in value)
            {
                writer.WriteName(outerKvp.Key.ToString());
                writer.WriteStartDocument();
                foreach(var innerKvp in outerKvp.Value)
                {
                    writer.WriteName(innerKvp.Key.ToString());
                    BsonSerializer.Serialize(writer, innerKvp.Value);
                }
                writer.WriteEndDocument();
            }
            writer.WriteEndDocument();
        }

        // Implement general object deserialization method required by IBsonSerializer
        public Dictionary<int, Dictionary<Guid, ActivityInstance>> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var result = new Dictionary<int, Dictionary<Guid, ActivityInstance>>();
            var reader = context.Reader;
            
            reader.ReadStartDocument();
            while (reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                var key = int.Parse(reader.ReadName());
                var innerDict = new Dictionary<Guid, ActivityInstance>();

                reader.ReadStartDocument();
                while (reader.ReadBsonType() != BsonType.EndOfDocument)
                {
                    var innerKey = Guid.Parse(reader.ReadName());
                    var value = BsonSerializer.Deserialize<ActivityInstance>(reader);
                    innerDict.Add(innerKey, value);
                }
                reader.ReadEndDocument();

                result.Add(key, innerDict);
            }
            reader.ReadEndDocument();
            
            return result;
        }

        object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return Deserialize(context, args); // Call the type-specific Deserialize method 
        }

        void IBsonSerializer.Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            Serialize(context, args, (Dictionary<int, Dictionary<Guid, ActivityInstance>>)value); // Cast and call the type-specific Serialize method
        }
    }

    public class ActivityInstancesSingleDictionarySerializer : IBsonSerializer<Dictionary<int, ActivityInstance>>
    {
        public Type ValueType => typeof(Dictionary<int, ActivityInstance>);

        // Type-specific Serialize method
        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Dictionary<int, ActivityInstance> value)
        {
            var writer = context.Writer;
            writer.WriteStartDocument();
            foreach (var outerKvp in value)
            {
                writer.WriteName(outerKvp.Key.ToString());
                BsonSerializer.Serialize(writer, outerKvp.Value);
            }
            writer.WriteEndDocument();
        }

        // Implement general object deserialization method required by IBsonSerializer
        public Dictionary<int, ActivityInstance> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var result = new Dictionary<int, ActivityInstance>();
            var reader = context.Reader;

            reader.ReadStartDocument();
            while (reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                var key = int.Parse(reader.ReadName());
                var value = BsonSerializer.Deserialize<ActivityInstance>(reader);
                result.Add(key, value);
            }
            reader.ReadEndDocument();

            return result;
        }

        object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return Deserialize(context, args); // Call the type-specific Deserialize method 
        }

        void IBsonSerializer.Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            Serialize(context, args, (Dictionary<int, ActivityInstance>)value); // Cast and call the type-specific Serialize method
        }
    }
}
