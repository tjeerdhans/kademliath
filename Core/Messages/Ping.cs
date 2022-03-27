using MessagePack;

namespace Kademliath.Core.Messages
{
	/// <summary>
	/// Represents a ping message, used to see if a remote node is up.
	/// </summary>
	[MessagePackObject]
	public class Ping : Request
	{
		public Ping(Id senderId, Id conversationId) : base(senderId, conversationId)
		{
		}
		
		public override string GetName()
		{
			return "PING";
		}
	}
}
