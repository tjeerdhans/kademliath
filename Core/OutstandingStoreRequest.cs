using System;

namespace Kademliath.Core
{
    // The list of put requests we sent is more complex
    // We need to keep the data and timestamp, but don't want to insert it in our storage.
    // So we keep it in a cache, and discard it if it gets too old.
    internal struct OutstandingStoreRequest
    {
        public Id Key;
        public byte[] Val;
        public DateTime Publication;
        public DateTime Sent;
    }
}