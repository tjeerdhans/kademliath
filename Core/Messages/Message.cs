using System;

namespace Kademliath.Core.Messages
{
    /// <summary>
    /// Represents a generic DHT RPC message
    /// </summary>
    [Serializable]
    public abstract class Message
    {
        public Id SenderId { get;}
        public Id ConversationId { get; }

		/// <summary>
        /// Make a new message, recording the sender's Id.
        /// </summary>
        /// <param name="senderId"></param>
        protected Message(Id senderId) {
            SenderId = senderId;
            ConversationId = new Id();
        }
		
        /// <summary>
        /// Make a new message in a given conversation.
        /// </summary>
        /// <param name="senderId"></param>
        /// <param name="conversationId"></param>
        protected Message(Id senderId, Id conversationId) {
            SenderId = senderId;
            ConversationId = conversationId;
        }

        /// <summary>
        /// Get the name of the message.
        /// </summary>
        /// <returns></returns>
        public virtual string GetName()
        {
            throw new NotImplementedException();
        }		
    }
}