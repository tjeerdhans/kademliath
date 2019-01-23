using System;
using System.Collections.Generic;

namespace Core.Messages
{
	/// <summary>
	/// Description of FindKeyContactResponse.
	/// </summary>
	[Serializable]
	public class FindValueContactResponse : Response
	{
		private readonly List<Contact> _contacts;
		
		/// <summary>
		/// Make a new response reporting contacts to try.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="request"></param>
		/// <param name="close"></param>
		public FindValueContactResponse(Id nodeId, FindValue request, List<Contact> close) : base(nodeId, request)
		{
			_contacts = close;
		}
		
		/// <summary>
		/// Return the list of contacts sent.
		/// </summary>
		/// <returns></returns>
		public List<Contact> GetContacts()
		{
			return _contacts;
		}
		
		public override string GetName()
		{
			return "FIND_VALUE_RESPONSE_CONTACTS";
		}
	}
}
