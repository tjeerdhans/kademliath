using MessagePack;

namespace Kademliath.Core.Messages.Responses
{
    /// <summary>
    /// A reply to a store query.
    /// Say if we're willing to store the data, and if we already have it.
    /// </summary>
    [MessagePackObject]
    public class StoreResponse : Response
    {
        [Key(2)] public bool ShouldSendData { get; }

        public StoreResponse(Id nodeId, Id respondingToConversationId, bool shouldSendData) : base(nodeId, respondingToConversationId)
        {
            ShouldSendData = shouldSendData;
        }

        public override string GetName()
        {
            return "STORE_RESPONSE";
        }
    }
}