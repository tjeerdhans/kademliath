using MessagePack;

namespace Kademliath.Core.Messages.Responses
{
	/// <summary>
	/// Represents a ping reply.
	/// </summary>
	[MessagePackObject]
	public class Pong : Response
	{
		public Pong(Id senderId, Id respondingToConversationId) : base(senderId, respondingToConversationId)
		{
		}
		
		public override string GetName()
		{
			return "PONG";
		}
	}
}
