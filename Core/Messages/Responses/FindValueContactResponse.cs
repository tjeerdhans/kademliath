using System;
using System.Collections.Generic;
using MessagePack;

namespace Kademliath.Core.Messages.Responses
{
    /// <summary>
    /// Description of FindKeyContactResponse.
    /// </summary>
    [MessagePackObject]
    public class FindValueContactResponse : Response
    {
        [Key(2)] public List<Contact> Contacts { get; }

        /// <summary>
        /// Make a new response reporting contacts to try.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="respondingToConversationId"></param>
        /// <param name="closeByContacts"></param>
        public FindValueContactResponse(Id nodeId, Id respondingToConversationId, List<Contact> closeByContacts) : base(
            nodeId, respondingToConversationId)
        {
            Contacts = closeByContacts;
        }

        public override string GetName()
        {
            return "FIND_VALUE_RESPONSE_CONTACTS";
        }
    }
}