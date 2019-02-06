using System;

namespace Kademliath.Core.Messages
{
	/// <summary>
	/// Represents a ping message, used to see if a remote node is up.
	/// </summary>
	[Serializable]
	public class Ping : Message
	{
		public Ping(Id senderId) : base(senderId)
		{
		}
		
		public override string GetName()
		{
			return "PING";
		}
	}
}
