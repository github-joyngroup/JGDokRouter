using ProtoBuf;
using System.Text.Json.Serialization;

namespace Joyn.LLMDriver.Models
{
    [ProtoContract]
    public class ActivityModel
    {
        /// <summary>Identifies the transaction that is being executed - will be shared across modules</summary>
        [ProtoMember(1)]
        public Guid TransactionIdentifier { get; set; }

        /// <summary>Identifies the Domain that is being executed</summary>
        [ProtoMember(2)]
        public Guid DomainIdentifier { get; set; }

        /// <summary>Path where assets will be written to</summary>
        [ProtoMember(3)]
        public string BaseAssetsFilePath { get; set; } = string.Empty;

        /// <summary>Identifies the record within the database where the extended model will be stored</summary>
        [ProtoMember(4)]
        public string DatabaseIdentifier { get; set; } = string.Empty;

        /// <summary>Identifies the company this activity model relates to</summary>
        /// <remarks>EPocas, I don't like this approach, it should be kind of context key or it should be within the database record
        /// TODO: Improve this</remarks>
        [ProtoMember(5)]
        public string CompanyIdentifier { get; set; } = string.Empty;
    }
}
