using System;

namespace Core.Messages
{
	/// <summary>
	/// Send along the data in response to an affirmative StoreResponse.
	/// </summary>
	[Serializable]
	public class StoreData : Response
	{
		private Id key;
		private Id dataHash; // Distinguish multiple values for a given key
		private object data;
		private DateTime publication;
		
		/// <summary>
		/// Make a mesage to store the given data.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="request"></param>
		/// <param name="theKey"></param>
		/// <param name="theDataHash"></param>
		/// <param name="theData"></param>
		/// <param name="originalPublication"></param>
		public StoreData(Id nodeId, StoreResponse request, Id theKey, Id theDataHash, object theData, DateTime originalPublication) : base(nodeId, request)
		{
			key = theKey;
			data = theData;
			dataHash = theDataHash;
			publication = originalPublication;
		}
		
		/// <summary>
		/// Return the key we want to store at.
		/// </summary>
		/// <returns></returns>
		public Id GetKey()
		{
			return key;
		}
		
		/// <summary>
		/// Get the data to store.
		/// </summary>
		/// <returns></returns>
		public object GetData()
		{
			return data;
		}
		
		/// <summary>
		/// Gets the data value hash.
		/// </summary>
		/// <returns></returns>
		public Id GetDataHash()
		{
			return dataHash;
		}
		
		/// <summary>
		/// Get when the data was originally published, in UTC.
		/// </summary>
		/// <returns></returns>
		public DateTime GetPublicationTime()
		{
			return publication.ToUniversalTime();
		}
		
		public override string GetName()
		{
			return "STORE_DATA";
		}
	}
}
