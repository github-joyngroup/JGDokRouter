using Joyn.DokRouter.Common.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter.MongoDAL
{
    public class DictionarySerializer<TDictionary, KeySerializer, ValueSerializer> : DictionarySerializerBase<TDictionary>
        where TDictionary : class, IDictionary, new()
        where KeySerializer : IBsonSerializer, new()
        where ValueSerializer : IBsonSerializer, new()
    {
        public DictionarySerializer() : base(DictionaryRepresentation.Document, new KeySerializer(), new ValueSerializer())
        {
        }

        protected override TDictionary CreateInstance()
        {
            return new TDictionary();
        }
    }
}
