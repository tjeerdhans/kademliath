using System;
using System.Collections.Generic;

namespace Kademliath.Core.Messages
{
	/// <summary>
	/// Description of FindKeyContactResponse.
	/// </summary>
	[Serializable]
	public class FindValueContactResponse : Response
	{
		public List<Contact> Contacts { get; }
		
		/// <summary>
		/// Make a new response reporting contacts to try.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="request"></param>
		/// <param name="closeByContacts"></param>
		public FindValueContactResponse(Id nodeId, FindValue request, List<Contact> closeByContacts) : base(nodeId, request)
		{
			Contacts = closeByContacts;
		}
		
		public override string GetName()
		{
			return "FIND_VALUE_RESPONSE_CONTACTS";
		}
	}
}
