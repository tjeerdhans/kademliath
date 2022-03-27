using System.Collections.Generic;
using MessagePack;

namespace Kademliath.Core.Messages.Responses
{
    /// <summary>
    /// Send data in reply to a FindValue
    /// </summary>
    [MessagePackObject]
    public class FindValueDataResponse : Response
    {
        [Key(2)] public List<byte[]> Values { get; }

        /// <summary>
        /// Make a new response.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="respondingToConversationId"></param>
        /// <param name="data"></param>
        public FindValueDataResponse(Id nodeId, Id respondingToConversationId, List<byte[]> data) : base(nodeId, respondingToConversationId)
        {
            Values = data;
        }

        public override string GetName()
        {
            return "FIND_VALUE_RESPONSE_DATA";
        }
    }
}