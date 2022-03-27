using Kademliath.Core.Messages.Responses;
using MessagePack;

namespace Kademliath.Core.Messages;

[Union(0, typeof(Request))]
[Union(1, typeof(Response))]
[Union(2, typeof(Ping))]
[Union(3, typeof(Pong))]
[Union(4, typeof(StoreQuery))]
[Union(5, typeof(FindValue))]
[Union(6, typeof(FindValueDataResponse))]
[Union(7, typeof(FindValueContactResponse))]
[Union(8, typeof(FindNode))]
[Union(9, typeof(FindNodeResponse))]
[Union(10, typeof(StoreData))]
[Union(11, typeof(StoreResponse))]
[MessagePackObject]
public abstract class Message
{
    [Key(0)] public Id SenderId { get; protected init; }
    [Key(1)] public Id ConversationId { get; protected init; }

    public abstract string GetName();
}