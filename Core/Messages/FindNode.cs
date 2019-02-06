using System;

namespace Kademliath.Core.Messages
{
	/// <summary>
	/// A message used to search for a node.
	/// </summary>
	[Serializable]
	public class FindNode : Message
	{
		public Id Target { get; }
		
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
