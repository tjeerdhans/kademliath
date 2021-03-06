using System;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;

namespace Kademliath.Core
{
    /// <summary>
    /// Represents the information needed to contact another node.
    /// </summary>
    [Serializable]
    public class Contact
    {
        public Id NodeId { get; }

        /// <summary>
        /// The NodeEndPoint is a property that constructs and deconstructs an <see cref="IPEndPoint"/>
        /// because that type can't be serialized by <see cref="BinaryFormatter"/> in dotnet core.
        /// </summary>
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