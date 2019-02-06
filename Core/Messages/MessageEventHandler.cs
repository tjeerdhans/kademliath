namespace Kademliath.Core.Messages
{
    /// <summary>
    /// A delegate for handling message events.
    /// </summary>
    public delegate void MessageEventHandler<in T>(Contact sender, T message) where T : Message;
}