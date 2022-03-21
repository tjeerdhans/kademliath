using Kademliath.Core.Messages.Responses;
using MessagePack;

namespace Kademliath.Core.Messages;

[Union(0, typeof(Request))]
[Union(1, typeof(Response))]
[Union(2, typeof(Ping))]
[Union(3, typeof(StoreQuery))]
[Union(4, typeof(FindValue))]
[Union(5, typeof(FindNode))]
[Union(7, typeof(Pong))]
[Union(8, typeof(StoreData))]
[Union(9, typeof(StoreResponse))]
[Union(10, typeof(FindValueDataResponse))]
[Union(11, typeof(FindValueContactResponse))]
[Union(12, typeof(FindNodeResponse))]
[MessagePackObject]
public abstract class Message
{
    [Key(0)] public Id SenderId { get; protected init; }
    [Key(1)] public Id ConversationId { get; protected init; }

    public abstract string GetName();
}