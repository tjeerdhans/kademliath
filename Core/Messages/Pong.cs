using System;

namespace Core.Messages
{
	/// <summary>
	/// Represents a ping reply.
	/// </summary>
	[Serializable]
	public class Pong : Response
	{
		public Pong(Id senderId, Ping ping) : base(senderId, ping)
		{
		}
		
		public override string GetName()
		{
			return "PONG";
		}
	}
}
