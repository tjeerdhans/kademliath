using System;
using Kademliath.Core.Messages;

namespace Kademliath.Core
{
    internal struct CachedResponse
    {
        public Response Response;
        public DateTime Arrived;
    }
}