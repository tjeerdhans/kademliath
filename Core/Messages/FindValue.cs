using MessagePack;

namespace Kademliath.Core.Messages
{
    /// <summary>
    /// Represents a request to get a value.
    /// Receiver should either send key or a node list.
    /// </summary>
    [MessagePackObject]
    public class FindValue : Request
    {
        [Key(2)] public Id Key { get; }

        /// <summary>
        /// Make a new FindValue message.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="wantedKey"></param>
        /// <param name="conversationId"></param>
        public FindValue(Id nodeId, Id wantedKey, Id conversationId = null) : base(nodeId, conversationId)
        {
            Key = wantedKey;
        }

        public override string GetName()
        {
            return "FIND_VALUE";
        }
    }
}