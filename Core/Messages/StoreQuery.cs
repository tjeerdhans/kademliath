using System;

namespace Core.Messages
{
	/// <summary>
	/// A message asking if another node will store data for us, and if we need to send the data.
	/// Maybe they already have it.
	/// We have to send the timestamp with it for people to republish stuff.
	/// </summary>
	[Serializable]
	public class StoreQuery : Message
	{
		private readonly Id _key;
		private readonly Id _dataHash;
		private readonly DateTime _publication;
		private readonly int _valueSize;
		
		/// <summary>
		/// Make a new STORE_QUERY message.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="toStore"></param>
		/// <param name="hash">A hash of the data value</param>
		/// <param name="originalPublication"></param>
		/// <param name="dataSize"></param>
		public StoreQuery(Id nodeId, Id toStore, Id hash, DateTime originalPublication, int dataSize) : base(nodeId)
		{
			_key = toStore;
			_dataHash = hash;
			_publication = originalPublication;
			_valueSize = dataSize;
		}
		
		/// <summary>
		/// Returns the key that we want stored.
		/// </summary>
		/// <returns></returns>
		public Id GetKey()
		{
			return _key;
		}
		
		/// <summary>
		/// Gets the hash of the data value we're asking about.
		/// </summary>
		/// <returns></returns>
		public Id GetDataHash()
		{
			return _dataHash;
		}
		
		/// <summary>
		/// Returns the size of the value we're storing, in bytes
		/// </summary>
		/// <returns></returns>
		public int GetValueSize()
		{
			return _valueSize;
		}
		
		/// <summary>
		/// Get when the data was originally published, in UTC.
		/// </summary>
		/// <returns></returns>
		public DateTime GetPublicationTime()
		{
			return _publication.ToUniversalTime();
		}
		
		public override string GetName()
		{
			return "STORE_QUERY";
		}
	}
}
