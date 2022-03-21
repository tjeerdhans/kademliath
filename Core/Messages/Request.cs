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
        protected Request(Id senderId)
        {
            SenderId = senderId;
            ConversationId = new Id();
        }
    }
}