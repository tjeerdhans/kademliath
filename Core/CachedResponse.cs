using System;
using Kademliath.Core.Messages.Responses;

namespace Kademliath.Core
{
    internal struct CachedResponse
    {
        public Response Response;
        public DateTime Arrived;
    }
}