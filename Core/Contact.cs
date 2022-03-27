using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using MessagePack;

namespace Kademliath.Core
{
    /// <summary>
    /// Represents the information needed to contact another node.
    /// </summary>
    [MessagePackObject]
    public class Contact
    {
        [Key(0)] public Id NodeId { get; }

        /// <summary>
        /// The NodeEndPoint is a property that constructs and deconstructs an <see cref="IPEndPoint"/>
        /// because that type can't be serialized by <see cref="BinaryFormatter"/> in dotnet core.
        /// </summary>
        [IgnoreMember]
        public IPEndPoint NodeEndPoint => new(IPAddress.Parse(IpAddress), Port);

        [Key(1)] public string IpAddress { get; private set; }
        [Key(2)] public int Port { get; private set; }

        //[SerializationConstructor]
        public Contact(Id id, string ipAddress, int port)
        {
            NodeId = id;
            IpAddress = ipAddress;
            Port = port;
        }

        public override string ToString()
        {
            return NodeId + "@" + NodeEndPoint;
        }
    }
}