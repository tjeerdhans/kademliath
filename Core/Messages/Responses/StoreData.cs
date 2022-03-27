using System;
using MessagePack;

namespace Kademliath.Core.Messages.Responses
{
    /// <summary>
    /// Send along the data in response to an affirmative StoreResponse.
    /// </summary>
    [MessagePackObject]
    public class StoreData : Response
    {
        [Key(2)] public Id Key { get; }
        [Key(3)] public byte[] Data { get; }
        //private readonly Id _dataHash; // Distinguish multiple values for a given key
        [Key(4)] public DateTime PublicationTimestampUtc { get; }

        /// <summary>
        /// Make a message to store the given data.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="respondingToConversationId"></param>
        /// <param name="key"></param>
        /// <param name="dataHash"></param>
        /// <param name="data"></param>
        /// <param name="originalPublicationTimestampUtc"></param>
        public StoreData(Id nodeId, Id respondingToConversationId, Id key, byte[] data,
            DateTime originalPublicationTimestampUtc) : base(nodeId, respondingToConversationId)
        {
            Key = key;
            Data = data;
            //_dataHash = dataHash;
            PublicationTimestampUtc = originalPublicationTimestampUtc.ToUniversalTime();
        }

        public override string GetName()
        {
            return "STORE_DATA";
        }
    }
}