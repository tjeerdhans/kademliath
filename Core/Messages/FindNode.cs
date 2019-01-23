using System;

namespace Core.Messages
{
	/// <summary>
	/// A message used to search for a node.
	/// </summary>
	[Serializable]
	public class FindNode : Message
	{
		private Id target;
		
		/// <summary>
		/// Make a new FIND_NODE message
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="toFind"></param>
		public FindNode(Id nodeId, Id toFind) : base(nodeId)
		{
			target = toFind;
		}
		
		/// <summary>
		/// Get the target of this message.
		/// </summary>
		/// <returns></returns>
		public Id GetTarget()
		{
			return target;
		}
		
		public override string GetName() 
		{
			return "FIND_NODE";
		}
	}
}
