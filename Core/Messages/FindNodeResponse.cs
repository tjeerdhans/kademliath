using System;
using System.Collections.Generic;

namespace Kademliath.Core.Messages
{
    /// <summary>
    /// A response to a FindNode message.
    /// Contains a list of Contacts.
    /// </summary>
    [Serializable]
    public class FindNodeResponse : Response
    {
        public List<Contact> RecommendedContacts { get; }

        public FindNodeResponse(Id nodeId, FindNode request, List<Contact> recommended) : base(nodeId, request)
        {
            RecommendedContacts = recommended;
        }

        public override string GetName()
        {
            return "FIND_NODE_RESPONSE";
        }
    }
}