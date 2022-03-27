using MessagePack;

namespace Kademliath.Core.Messages
{
    /// <summary>
    /// Represents a generic DHT RPC message
    /// </summary>
    [MessagePackObject]
    public abstract class Request : Message
    {

        /// <summary>
        /// Make a new message, recording the sender's Id.
        /// </summary>
        /// <param name="senderId"></param>
        /// <param name="conversationId"></param>
        protected Request(Id senderId, Id conversationId)
        {
            SenderId = senderId;
            ConversationId = conversationId;
        }
    }
}