using System;
using System.Net;

namespace Core
{
    /// <summary>
    /// Represents the information needed to contact another node.
    /// </summary>
    [Serializable]
    public class Contact
    {
        private readonly Id _nodeId;
        private readonly IPEndPoint _nodeEndpoint;
		
        /// <summary>
        /// Make a contact for a node with the given ID at the given location.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="endpoint"></param>
        public Contact(Id id, IPEndPoint endpoint)
        {
            _nodeId = id;
            _nodeEndpoint = endpoint;
        }
		
        /// <summary>
        /// Get the node's ID.
        /// </summary>
        /// <returns></returns>
        public Id GetId() {
            return _nodeId;
        }
		
        /// <summary>
        /// Get the node's endpoint.
        /// </summary>
        /// <returns></returns>
        public IPEndPoint GetEndPoint() {
            return _nodeEndpoint;
        }
		
        public override string ToString()
        {
            return GetId() + "@" + GetEndPoint();
        }
    }
}