using System;

namespace Core.Messages
{
    /// <summary>
    /// Represents a generic DHT RPC message
    /// </summary>
    [Serializable]
    public abstract class Message
    {
        // All messages include sender id
        private readonly Id _senderId;
        private readonly Id _conversationId;
		
        /// <summary>
        /// Make a new message, recording the sender's Id.
        /// </summary>
        /// <param name="senderId"></param>
        public Message(Id senderId) {
            _senderId = senderId;
            _conversationId = Id.RandomId();
        }
		
        /// <summary>
        /// Make a new message in a given conversation.
        /// </summary>
        /// <param name="senderId"></param>
        /// <param name="conversationId"></param>
        public Message(Id senderId, Id conversationId) {
            _senderId = senderId;
            _conversationId = conversationId;
        }
		
        /// <summary>
        /// Get the name of the message.
        /// </summary>
        /// <returns></returns>
        public abstract string GetName();
		
        /// <summary>
        /// Get the Id of the sender of the message.
        /// </summary>
        /// <returns></returns>
        public Id GetSenderId() {
            return _senderId;
        }
		
        /// <summary>
        /// Gets the Id of this conversation.
        /// </summary>
        /// <returns></returns>
        public Id GetConversationId() {
            return _conversationId;
        }
    }
}