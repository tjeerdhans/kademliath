using System;
using MessagePack;

namespace Kademliath.Core.Messages
{
    /// <summary>
    /// A message asking if another node will store data for us, and if we need to send the data.
    /// Maybe they already have it.
    /// We have to send the timestamp with it for people to republish stuff.
    /// </summary>
    [MessagePackObject]
    public class StoreQuery : Request
    {
        [Key(2)] public Id Key { get; }
        [Key(3)] public Id DataHash { get; }
        [Key(4)] public DateTime PublicationUtc { get;}

        /// <summary>
        /// Make a new STORE_QUERY message.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="toStore"></param>
        /// <param name="hash">A hash of the data value</param>
        /// <param name="originalPublicationTimestamp"></param>
        /// <param name="conversationId"></param>
        public StoreQuery(Id nodeId, Id conversationId, Id toStore, Id hash, DateTime originalPublicationTimestamp) :
            base(nodeId, conversationId)
        {
            Key = toStore;
            DataHash = hash;
            PublicationUtc = originalPublicationTimestamp.ToUniversalTime();
        }
        
        public override string GetName()
        {
            return "STORE_QUERY";
        }
    }
}