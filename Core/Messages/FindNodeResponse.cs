using System;
using System.Collections.Generic;

namespace Core.Messages
{
	/// <summary>
	/// A response to a FindNode message.
	/// Contains a list of Contacts.
	/// </summary>
	[Serializable]
	public class FindNodeResponse : Response
	{
		private readonly List<Contact> _contacts;
		
		public FindNodeResponse(Id nodeId, FindNode request, List<Contact> recommended) : base(nodeId, request)
		{
			_contacts = recommended;
		}
		
		/// <summary>
		/// Gets the list of recommended contacts.
		/// </summary>
		/// <returns></returns>
		public List<Contact> GetContacts()
		{
			return _contacts;
		}
		
		public override string GetName()
		{
			return "FIND_NODE_RESPONSE";
		}
	}
}
