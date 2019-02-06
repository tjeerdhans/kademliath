using System;

namespace Kademliath.Core.Messages
{
    /// <summary>
    /// A reply to a store query.
    /// Say if we're willing to store the data, and if we already have it.
    /// </summary>
    [Serializable]
    public class StoreResponse : Response
    {
        public bool ShouldSendData { get; }

        public StoreResponse(Id nodeId, StoreQuery query, bool shouldSendData) : base(nodeId, query)
        {
            ShouldSendData = shouldSendData;
        }

        public override string GetName()
        {
            return "STORE_RESPONSE";
        }
    }
}