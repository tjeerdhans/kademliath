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
        private readonly Id _dataHash; // Distinguish multiple values for a given key
        private readonly DateTime _publicationTimestamp;

        /// <summary>
        /// Make a message to store the given data.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="respondingToConversationId"></param>
        /// <param name="key"></param>
        /// <param name="dataHash"></param>
        /// <param name="data"></param>
        /// <param name="originalPublicationTimestamp"></param>
        public StoreData(Id nodeId, Id respondingToConversationId, Id key, Id dataHash, byte[] data,
            DateTime originalPublicationTimestamp) : base(nodeId, respondingToConversationId)
        {
            Key = key;
            Data = data;
            _dataHash = dataHash;
            _publicationTimestamp = originalPublicationTimestamp;
        }

        /// <summary>
        /// Get when the data was originally published, in UTC.
        /// </summary>
        /// <returns></returns>
        public DateTime GetPublicationTimeUtc()
        {
            return _publicationTimestamp.ToUniversalTime();
        }

        public override string GetName()
        {
            return "STORE_DATA";
        }
    }
}