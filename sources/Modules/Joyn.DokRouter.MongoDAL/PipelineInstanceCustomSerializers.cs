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
    /*
    public class ActivityExecutionKeySerializer : IBsonSerializer<ActivityExecutionKey>
    {
        public Type ValueType => typeof(ActivityExecutionKey);

        // Type-specific Serialize method
        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, ActivityExecutionKey value)
        {
            byte[] bytes = new byte[64]; // 16 bytes per GUID x 4
            value.PipelineInstanceKey.PipelineDefinitionIdentifier.ToByteArray().CopyTo(bytes, 0);
            value.PipelineInstanceKey.PipelineInstanceIdentifier.ToByteArray().CopyTo(bytes, 16);
            value.ActivityDefinitionIdentifier.ToByteArray().CopyTo(bytes, 32);
            value.ActivityExecutionIdentifier.ToByteArray().CopyTo(bytes, 48);

            context.Writer.WriteString(Convert.ToBase64String(bytes));
        }

        // Implement general object deserialization method required by IBsonSerializer
        public ActivityExecutionKey Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var stringValue = context.Reader.ReadString();
            byte[] bytes = Convert.FromBase64String(stringValue);
            ActivityExecutionKey instance = new ActivityExecutionKey
            {
                PipelineInstanceKey = new PipelineInstanceKey
                {
                    PipelineDefinitionIdentifier = new Guid(new Span<byte>(bytes, 0, 16)),
                    PipelineInstanceIdentifier = new Guid(new Span<byte>(bytes, 16, 16))
                },
                ActivityDefinitionIdentifier = new Guid(new Span<byte>(bytes, 32, 16)),
                ActivityExecutionIdentifier = new Guid(new Span<byte>(bytes, 48, 16))
            };

            return instance;
        }

        object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return Deserialize(context, args); // Call the type-specific Deserialize method 
        }

        void IBsonSerializer.Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            Serialize(context, args, (ActivityExecutionKey)value); // Cast and call the type-specific Serialize method
        }
    }
*/

    public class ActivityExecutionsSerializer : IBsonSerializer<Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>>>
    {
        public Type ValueType => typeof(Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>>);

        // Type-specific Serialize method
        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>> value)
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
        public Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var result = new Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>>();
            var reader = context.Reader;
            
            reader.ReadStartDocument();
            while (reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                var key = int.Parse(reader.ReadName());
                var innerDict = new Dictionary<ActivityExecutionKey, ActivityExecution>();

                reader.ReadStartDocument();
                while (reader.ReadBsonType() != BsonType.EndOfDocument)
                {
                    var innerKey = ActivityExecutionKey.FromString(reader.ReadName());
                    var value = BsonSerializer.Deserialize<ActivityExecution>(reader);
                    innerDict.Add(innerKey, value);
                }
                reader.ReadEndDocument();

                result.Add(key, innerDict);
            }
            reader.ReadEndDocument();
            return new Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>>();
        }

        object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return Deserialize(context, args); // Call the type-specific Deserialize method 
        }

        void IBsonSerializer.Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            Serialize(context, args, (Dictionary<int, Dictionary<ActivityExecutionKey, ActivityExecution>>)value); // Cast and call the type-specific Serialize method
        }
    }
}
