using System.Collections.Generic;
using MessagePack;

namespace Kademliath.Core.Messages.Responses
{
    /// <summary>
    /// A response to a FindNode message.
    /// Contains a list of Contacts.
    /// </summary>
    [MessagePackObject]
    public class FindNodeResponse : Response
    {
        [Key(2)] public List<Contact> RecommendedContacts { get; }

        public FindNodeResponse(Id nodeId, Id respondingToConversationId, List<Contact> recommended) : base(nodeId,
            respondingToConversationId)
        {
            RecommendedContacts = recommended;
        }

        public override string GetName()
        {
            return "FIND_NODE_RESPONSE";
        }
    }
}