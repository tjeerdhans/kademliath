using System;
using System.Net;
using Kademliath.Core;

namespace Core
{
    /// <summary>
    /// Represents the information needed to contact another node.
    /// </summary>
    [Serializable]
    public class Contact
    {
        public Id NodeId { get; }

        public IPEndPoint NodeEndPoint
        {
            get => new IPEndPoint(IPAddress.Parse(IpAddress), Port);
            private set
            {
                IpAddress = value.Address.ToString();
                Port = value.Port;
            }
        }

        public string IpAddress { get; private set; }
        public int Port { get; private set; }

        public Contact(Id id, string ipAddress, int port)
        {
            NodeId = id;
            IpAddress = ipAddress;
            Port = port;
        }

        public Contact(Id id, IPEndPoint ipEndPoint)
        {
            NodeId = id;
            NodeEndPoint = ipEndPoint;
        }

        public override string ToString()
        {
            return NodeId + "@" + NodeEndPoint;
        }
    }
}