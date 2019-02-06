using System;

namespace Kademliath.Core.Messages
{
    /// <summary>
    /// Send along the data in response to an affirmative StoreResponse.
    /// </summary>
    [Serializable]
    public class StoreData : Response
    {
        public Id Key { get; }
        private readonly Id _dataHash; // Distinguish multiple values for a given key
        public object Data { get; }
        private readonly DateTime _publicationTimestamp;

        /// <summary>
        /// Make a message to store the given data.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="request"></param>
        /// <param name="key"></param>
        /// <param name="dataHash"></param>
        /// <param name="data"></param>
        /// <param name="originalPublicationTimestamp"></param>
        public StoreData(Id nodeId, StoreResponse request, Id key, Id dataHash, object data,
            DateTime originalPublicationTimestamp) : base(nodeId, request)
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