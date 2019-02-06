using System;

namespace Kademliath.Core.Messages
{
    /// <summary>
    /// Represents a response message, in the same conversation as an original message.
    /// </summary>
    [Serializable]
    public abstract class Response : Message
    {
        /// <summary>
        /// Make a reply in the same conversation as the given message.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="respondingTo"></param>
        protected Response(Id nodeId, Message respondingTo) : base(nodeId, respondingTo.ConversationId)
        {
        }
    }
}