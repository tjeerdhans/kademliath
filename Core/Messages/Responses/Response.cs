using MessagePack;

namespace Kademliath.Core.Messages.Responses
{
    /// <summary>
    /// Represents a response message, in the same conversation as an original message.
    /// </summary>
    [MessagePackObject]
    public abstract class Response : Message
    {
        /// <summary>
        /// Make a reply in the same conversation as the given message.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="respondingToConversationId"></param>
        protected Response(Id nodeId, Id respondingToConversationId)
        {
            SenderId = nodeId;
            ConversationId = respondingToConversationId;
        }
    }
}