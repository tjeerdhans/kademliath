using MessagePack;

namespace Kademliath.Core.Messages
{
    /// <summary>
    /// A message used to search for a node.
    /// </summary>
    [MessagePackObject]
    public class FindNode : Request
    {
       [Key(2)] public Id Target { get; }

        /// <summary>
        /// Make a new FIND_NODE message
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="toFind"></param>
        public FindNode(Id nodeId, Id toFind) : base(nodeId)
        {
            Target = toFind;
        }

        public override string GetName()
        {
            return "FIND_NODE";
        }
    }
}