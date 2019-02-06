using System;
using System.Collections.Generic;

namespace Kademliath.Core.Messages
{
    /// <summary>
    /// Send data in reply to a FindValue
    /// </summary>
    [Serializable]
    public class FindValueDataResponse : Response
    {
        public List<object> Values { get; }

        /// <summary>
        /// Make a new response.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="request"></param>
        /// <param name="data"></param>
        public FindValueDataResponse(Id nodeId, FindValue request, List<object> data) : base(nodeId, request)
        {
            Values = data;
        }

        public override string GetName()
        {
            return "FIND_VALUE_RESPONSE_DATA";
        }
    }
}