using System;

namespace Core.Messages
{
	/// <summary>
	/// A reply to a store query.
	/// Say if we're willing to store the data, and if we already have it.
	/// </summary>
	[Serializable]
	public class StoreResponse : Response
	{
		bool sendData;
		
		public StoreResponse(Id nodeId, StoreQuery query, bool accept) : base(nodeId, query)
		{
			sendData = accept;
		}
		
		/// <summary>
		/// Returns true if we should send them the data.
		/// </summary>
		/// <returns></returns>
		public bool ShouldSendData()
		{
			return sendData;
		}
		
		public override string GetName()
		{
			return "STORE_RESPONSE";
		}
	}
}
