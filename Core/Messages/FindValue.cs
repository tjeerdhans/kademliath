using System;

namespace Core.Messages
{
	/// <summary>
	/// Represents a request to get a value.
	/// Reciever should either send key or a node list.
	/// </summary>
	[Serializable]
	public class FindValue : Message
	{
		private readonly Id _key;
		
		/// <summary>
		/// Make a new FindValue message.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="wantedKey"></param>
		public FindValue(Id nodeId, Id wantedKey) : base(nodeId)
		{
			_key = wantedKey;
		}
		
		/// <summary>
		/// Return the key this message wants.
		/// </summary>
		/// <returns></returns>
		public Id GetKey() {
			return _key;
		}
		
		public override string GetName()
		{
			return "FIND_VALUE";
		}
	}
}
