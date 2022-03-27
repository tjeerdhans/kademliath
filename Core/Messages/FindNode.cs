using MessagePack;

namespace Kademliath.Core.Messages
{
    /// <summary>
    /// A message used to search for a node.
    /// </summary>
    [MessagePackObject]
    public class FindNode : Request
    {
        [Key(2)] public Id Target { get; init; }

        /// <summary>
        /// Make a new FIND_NODE message
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="toFind"></param>
        /// <param name="conversationId"></param>
        public FindNode(Id nodeId, Id conversationId, Id toFind) : base(nodeId, conversationId)
        {
            Target = toFind;
        }

        public override string GetName()
        {
            return "FIND_NODE";
        }
    }
}