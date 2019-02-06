using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Kademliath.Core.Messages;

namespace Kademliath.Core
{
    /// <summary>
    /// Functions as a peer in the overlay network.
    /// </summary>
    public class KademliaNode : IDisposable
    {
        // Identity
        public Id NodeId { get; }

        // Network State
        private readonly Buckets _buckets;
        private Thread _bucketMinderThread; // Handle updates to cache
        private const int CheckInterval = 1;

        private readonly ConcurrentQueue<Contact> _contactQueue; // Add contacts here to be considered for caching
        private const int MaxQueueLength = 10;

        // Response cache
        // We want to be able to synchronously wait for responses, so we have other threads put them in this cache.
        // We also need to discard old ones.
        private readonly SortedList<Id, CachedResponse> _responseCache;
        private readonly TimeSpan _maxSyncWait = TimeSpan.FromMilliseconds(500);

        private readonly LocalStorage _storage; // Keep our key/value pairs

        // Store a list of what put requests we actually accepted while waiting for data.
        private readonly SortedList<Id, DateTime> _acceptedStoreRequests;

        private readonly SortedList<Id, OutstandingStoreRequest> _sentStoreRequests;

        // We need a thread to go through and expire all these things
        private Thread _conversationMinderThread;
        private static readonly TimeSpan MaxCacheTime = new TimeSpan(0, 0, 30);

        // How much clock skew do we tolerate?
        private readonly TimeSpan _maxClockSkew = new TimeSpan(1, 0, 0);

        // Kademlia config
        private const int
            Parallelism =
                3; // 'alpha' in the protocol specification. Number of requests to run at once for iterative operations.

        private const int NodesToFind = Buckets.BucketSize; // = k = bucket size

        private static readonly TimeSpan
            ExpireTime = new TimeSpan(24, 0, 0); // Time since original publication to expire a value

        private static readonly TimeSpan
            RefreshTime = new TimeSpan(1, 0, 0); // Time since last bucket access to refresh a bucket

        private static readonly TimeSpan
            ReplicateTime = new TimeSpan(1, 0, 0); // Interval at which we should re-insert our whole database

        private DateTime _lastReplication;
        //private static TimeSpan _republishTime = new TimeSpan(23, 0, 0); // Interval at which we should re-insert our values with new publication times

        // How often do we run high-level maintenance (expiration, etc.)
        private static readonly TimeSpan MaintenanceInterval = new TimeSpan(0, 1, 0);
        private Thread _maintenanceMinderThread;

        // Network IO
        private UdpClient _udpClient;
        private readonly int _port;
        private Thread _udpClientMinderThread;

        // Events
        public event MessageEventHandler<Message> GotMessage;
        public event MessageEventHandler<Response> GotResponse;
        public event MessageEventHandler<Ping> GotPing;
        public event MessageEventHandler<Pong> GotPong;
        public event MessageEventHandler<FindNode> GotFindNode;
        public event MessageEventHandler<FindNodeResponse> GotFindNodeResponse;
        public event MessageEventHandler<FindValue> GotFindValue;
        public event MessageEventHandler<FindValueContactResponse> GotFindValueContactResponse;
        public event MessageEventHandler<FindValueDataResponse> GotFindValueDataResponse;
        public event MessageEventHandler<StoreQuery> GotStoreQuery;
        public event MessageEventHandler<StoreResponse> GotStoreResponse;
        public event MessageEventHandler<StoreData> GotStoreData;

        private bool _debug;

        // Thread management 
        private bool _threadsRun = true;

        #region IDisposable Members

        /// <summary> 
        /// Performs actions needed to correctly finalize an instance. 
        /// </summary> 
        public void Dispose()
        {
            // Note that this class isn't using any unmanaged  
            // resources so we don't actually deallocate anything. All that 
            // really happens here is obstacles to a conventional finalize  
            // are removed. 

            // Shutdown the network. 
            _udpClient.Close();

            // Shutdown worker threads. 
            _threadsRun = false;

            // Wait for each thread to stop. 
            _bucketMinderThread.Join();
            _conversationMinderThread.Join();
            _udpClientMinderThread.Join();
            _maintenanceMinderThread.Join();
        }

        #endregion

        #region Setup

        /// <summary>
        /// Make a node on a random available port, using an Id specific to this machine.
        /// </summary>
        public KademliaNode()
            : this(0, Id.HostId())
        {
            // Nothing to do!
        }

        /// <summary>
        /// Make a node with a specified Id.
        /// </summary>
        /// <param name="id"></param>
        public KademliaNode(Id id)
            : this(0, id)
        {
            // Nothing to do!
        }

        /// <summary>
        /// Make a node on a specified port.
        /// </summary>
        /// <param name="port"></param>
        public KademliaNode(int port)
            : this(port, Id.HostId())
        {
            // Nothing to do!
        }

        /// <summary>
        /// Make a node on a specific port, with a specified Id
        /// </summary>
        /// <param name="port"></param>
        /// <param name="id"></param>
        public KademliaNode(int port, Id id)
        {
            // Set up all our data
            NodeId = id;
            _port = port;
            _buckets = new Buckets(NodeId);
            _contactQueue = new ConcurrentQueue<Contact>();
            _storage = new LocalStorage();
            _acceptedStoreRequests = new SortedList<Id, DateTime>();
            _sentStoreRequests = new SortedList<Id, OutstandingStoreRequest>();
            _responseCache = new SortedList<Id, CachedResponse>();
            _lastReplication = default(DateTime);

            WireUpEventHandlers();

            Connect();
            
            StartWorkerThreads();          
        }

        private void Connect()
        {
            _udpClient = new UdpClient();

            // create a socket that listens on all addresses (IPv4 _and_ IPv6)
            // http://blogs.msdn.com/b/malarch/archive/2005/11/18/494769.aspx
            Socket sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            sock.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName) 27, 0);
            sock.Bind(new IPEndPoint(IPAddress.IPv6Any, _port));
            _udpClient.Client = sock;
        }

        private void StartWorkerThreads()
        {
            _bucketMinderThread = new Thread(MindBuckets) {IsBackground = true};
            _bucketMinderThread.Start();

            _conversationMinderThread = new Thread(MindCaches) {IsBackground = true};
            _conversationMinderThread.Start();

            _udpClientMinderThread = new Thread(MindUdpClient) {IsBackground = true};
            _udpClientMinderThread.Start();

            _maintenanceMinderThread = new Thread(MindMaintenance) {IsBackground = true};
            _maintenanceMinderThread.Start();
        }

        private void WireUpEventHandlers()
        {
            GotMessage += HandleMessage;
            GotResponse += CacheResponse;

            GotPing += HandlePing;
            GotFindNode += HandleFindNode;
            GotFindValue += HandleFindValue;
            GotStoreQuery += HandleStoreQuery;
            GotStoreResponse += HandleStoreResponse;
            GotStoreData += HandleStoreData;
        }

        /// <summary>
        /// Bootstrap the node by having it ping another node. Returns true if we get a response.
        /// </summary>
        /// <param name="targetEndPoint"></param>
        /// <returns>True if bootstrapping was successful.</returns>
        public bool Bootstrap(IPEndPoint targetEndPoint)
        {
            // Send a blocking ping.
            var success = SyncPing(targetEndPoint);
            //Thread.Sleep(CheckInterval); // Wait for them to notice us
            return success;
        }

        /// <summary>
        /// Join the network.
        /// Assuming we have some contacts in our cache, get more by IterativeFindNoding ourselves.
        /// Then, refresh most (TODO: all) buckets.
        /// Returns true if we are connected after all that, false otherwise.
        /// </summary>
        public bool JoinNetwork()
        {
            Log("Joining");
            var found = IterativeFindNode(NodeId);
            if (found == null)
            {
                Log("Found <null list>");
            }
            else
            {
                foreach (Contact c in found)
                {
                    Log("Found contact: " + c);
                }
            }


            // Should get very nearly all of them
            // RefreshBuckets(); // Put this off until first maintenance.
            if (_buckets.GetCount() > 0)
            {
                Log("Joined");
                return true;
            }
            else
            {
                Log("Failed to join! No other nodes known!");
                return false;
            }
        }

        #endregion

        #region Interface

        /// <summary>
        /// Enables debugging output for the node.
        /// </summary>
        public void EnableDebug()
        {
            _debug = true;
        }

        /// <summary>
        /// Return the port we listen on.
        /// </summary>
        /// <returns></returns>
        public int GetPort()
        {
            return ((IPEndPoint) _udpClient.Client.LocalEndPoint).Port;
        }

        /// <summary>
        /// Store something in the DHT as the original publisher.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        public void Put(Id key, object val)
        {
            IterativeStore(key, val, DateTime.Now);
            // TODO: republish
        }

        /// <summary>
        /// Gets values for a key from the DHT. Returns the values or an empty list.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IList<object> Get(Id key)
        {
            if (_storage.ContainsKey(key))
            {
                // Check the local storage first.
                return _storage.Get(key);
            }

            var found = IterativeFindValue(key, out _);
            if (found == null)
            {
                // Empty list for nothing found
                return new List<object>();
            }
            else
            {
                return found;
            }
        }

        #endregion

        #region Maintenance Operations

        /// <summary>
        /// Expire old data, replicate all data, refresh needy buckets.
        /// </summary>
        private void MindMaintenance()
        {
            while (_threadsRun)
            {
                Thread.Sleep(MaintenanceInterval);
                Log("Performing maintenance");
                // Expire old
                _storage.Expire();
                Log(_storage.GetKeys().Count + " keys stored.");

                // Replicate all if needed
                // We get our own lists to iterate
                if (DateTime.Now > _lastReplication.Add(ReplicateTime))
                {
                    Log("Replicating");
                    foreach (Id key in _storage.GetKeys())
                    {
                        foreach (Id valHash in _storage.GetHashes(key))
                        {
                            try
                            {
                                // ReSharper disable once PossibleInvalidOperationException
                                IterativeStore(key, _storage.Get(key, valHash),
                                    (DateTime) _storage.GetPublicationTime(key, valHash));
                            }
                            catch (Exception ex)
                            {
                                Log($"Could not replicate {key}/{_storage.Get(key, valHash)}/{valHash}: {ex}");
                            }
                        }
                    }

                    _lastReplication = DateTime.Now;
                }

                // Refresh any needy buckets
                RefreshBuckets();
                Log("Done");
            }
        }

        /// <summary>
        /// Look for nodes to go in buckets we haven't touched in a while.
        /// </summary>
        private void RefreshBuckets()
        {
            Log("Refreshing buckets");
            IList<Id> toLookup = _buckets.IdsForRefresh(RefreshTime);
            foreach (Id key in toLookup)
            {
                IterativeFindNode(key);
            }

            Log("Refreshed");
        }

        #endregion

        #region Iterative Operations

        /// <summary>
        /// Do an iterativeStore operation and publish the key/value pair on the network
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <param name="originalInsertion"></param>
        private void IterativeStore(Id key, object val, DateTime originalInsertion)
        {
            // Find the K closest nodes to the key
            IList<Contact> closest = IterativeFindNode(key);
            Log("Storing at " + closest.Count + " nodes");
            foreach (Contact c in closest)
            {
                // Store a copy at each
                SyncStore(c.NodeEndPoint, key, val, originalInsertion);
            }
        }

        /// <summary>
        /// Do an iterativeFindNode operation.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        private IList<Contact> IterativeFindNode(Id target)
        {
            return IterativeFind(target, false, out _);
        }

        /// <summary>
        /// Do an iterativeFindValue.
        /// If we find values, we return them and put null in close.
        /// If we don't, we return null and put a list of close nodes in close.
        /// Note that this will NOT EVER CHECK THE LOCAL NODE! DO IT YOURSELF!
        /// </summary>
        /// <param name="target"></param>
        /// <param name="close"></param>
        /// <returns></returns>
        private IList<object> IterativeFindValue(Id target, out IList<Contact> close)
        {
            close = IterativeFind(target, true, out var found);
            return found;
        }

        /// <summary>
        /// Perform a Kademlia iterativeFind* operation.
        /// If getValue is true, it sends out a list of strings if values are found, or null none are.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="getValue">true for FindValue, false for FindNode</param>
        /// <param name="vals"></param>
        /// <returns></returns>
        private IList<Contact> IterativeFind(Id target, bool getValue, out IList<object> vals)
        {
            // Log the lookup
            if (target != NodeId)
            {
                _buckets.Touch(target);
            }

            // Get the alpha closest nodes to the target
            // TODO: Should actually pick from a certain bucket.
            SortedList<Id, Contact> shortlist = new SortedList<Id, Contact>();
            foreach (Contact c in _buckets.CloseContacts(Parallelism, target, null))
            {
                shortlist.Add(c.NodeId, c);
            }

            int shortlistIndex = 0; // Everyone before this is up.

            // Make an initial guess for the closest node
            Contact closest = null;
            foreach (Contact toAsk in shortlist.Values)
            {
                if (closest == null || (toAsk.NodeId ^ target) < (closest.NodeId ^ target))
                {
                    closest = toAsk;
                }
            }

            // Until we run out of people to ask or we're done...
            while (shortlistIndex < shortlist.Count && shortlistIndex < NodesToFind)
            {
                // Try the first alpha unexamined contacts
#pragma warning disable 219
                bool foundCloser = false; // TODO: WTF does the spec want with this
#pragma warning restore 219
                for (int i = shortlistIndex; i < shortlistIndex + Parallelism && i < shortlist.Count; i++)
                {
                    List<Contact> suggested;
                    if (getValue)
                    {
                        // Get list or value
                        suggested = SyncFindValue(shortlist.Values[i].NodeEndPoint, target, out var returnedValues);
                        if (returnedValues != null)
                        {
                            // We found it! Pass it up!
                            vals = returnedValues;
                            // But first, we have to store it at the closest node that doesn't have it yet.
                            // TODO: Actually do that. Not doing it now since we don't have the publish time.
                            return null;
                        }
                    }
                    else
                    {
                        // Only get list
                        suggested = SyncFindNode(shortlist.Values[i].NodeEndPoint, target);
                    }

                    if (suggested != null)
                    {
                        // Add suggestions to shortlist and check for closest
                        foreach (Contact suggestion in suggested)
                        {
                            if (!shortlist.ContainsKey(suggestion.NodeId))
                            {
                                // Contacts aren't value types so we have to do this.
                                shortlist.Add(suggestion.NodeId, suggestion);
                            }

                            if (closest != null && (suggestion.NodeId ^ target) < (closest.NodeId ^ target))
                            {
                                closest = suggestion;
                                // ReSharper disable once RedundantAssignment
                                foundCloser = true;
                            }
                        }
                    }
                    else
                    {
                        // Node down. Remove from shortlist and adjust loop indicies
                        shortlist.RemoveAt(i);
                        i--;
                        shortlistIndex--;
                    }
                }

                shortlistIndex += Parallelism;
            }

            // Drop extra ones
            // TODO: This isn't what the protocol says at all.
            while (shortlist.Count > NodesToFind)
            {
                shortlist.RemoveAt(shortlist.Count - 1);
            }

            vals = null;
            return shortlist.Values;
        }

        #endregion

        #region Synchronous Operations

        /// <summary>
        /// Try to store something at the given node.
        /// No return so we just pretend it's synchronous
        /// </summary>
        /// <param name="storeAt"></param>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <param name="originalInsertion"></param>
        private void SyncStore(IPEndPoint storeAt, Id key, object val, DateTime originalInsertion)
        {
            // Make a message
            var storeIt = new StoreQuery(NodeId, key, Id.Hash(val), originalInsertion, val.ToByteArray().Length);

            // Record having sent it
            var req = new OutstandingStoreRequest
            {
                Key = key, Val = val, Sent = DateTime.Now, Publication = originalInsertion
            };
            lock (_sentStoreRequests)
            {
                _sentStoreRequests[storeIt.ConversationId] = req;
            }

            // Send it
            SendMessage(storeAt, storeIt);
        }

        /// <summary>
        /// Send a FindNode request, and return an Id to retrieve its response.
        /// </summary>
        /// <param name="ask"></param>
        /// <param name="toFind"></param>
        /// <returns></returns>
        private List<Contact> SyncFindNode(IPEndPoint ask, Id toFind)
        {
            // Send message
            DateTime called = DateTime.Now;
            Message question = new FindNode(NodeId, toFind);
            SendMessage(ask, question);

            while (DateTime.Now < called.Add(_maxSyncWait))
            {
                // If we got a response, send it up
                FindNodeResponse resp = GetCachedResponse<FindNodeResponse>(question.ConversationId);
                if (resp != null)
                {
                    return resp.RecommendedContacts;
                }

                Thread.Sleep(CheckInterval); // Otherwise wait for one
            }

            return null; // Nothing in time
        }

        /// <summary>
        /// Send a synchronous FindValue.
        /// If we get a contact list, it gets returned.
        /// If we get data, it comes out in val and we return null.
        /// If we get nothing, we return null and val is null.
        /// </summary>
        /// <param name="ask"></param>
        /// <param name="toFind"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        private List<Contact> SyncFindValue(IPEndPoint ask, Id toFind, out IList<object> val)
        {
            // Send message
            var called = DateTime.Now;
            var question = new FindValue(NodeId, toFind);
            SendMessage(ask, question);

            while (DateTime.Now < called.Add(_maxSyncWait))
            {
                // See if we got data!
                FindValueDataResponse dataResp = GetCachedResponse<FindValueDataResponse>(question.ConversationId);
                if (dataResp != null)
                {
                    // Send it out and return null!
                    val = dataResp.Values;
                    return null;
                }

                // If we got a contact, send it up
                FindValueContactResponse resp =
                    GetCachedResponse<FindValueContactResponse>(question.ConversationId);
                if (resp != null)
                {
                    val = null;
                    return resp.Contacts;
                }

                Thread.Sleep(CheckInterval); // Otherwise wait for one
            }

            // Nothing in time
            val = null;
            return null;
        }

        /// <summary>
        /// Send a ping and wait for a response or a timeout.
        /// </summary>
        /// <param name="targetEndPoint"></param>
        /// <returns>true on a response, false otherwise</returns>
        private bool SyncPing(IPEndPoint targetEndPoint)
        {
            // Send message
            var now = DateTime.Now;
            var ping = new Ping(NodeId);
            SendMessage(targetEndPoint, ping);

            while (DateTime.Now < now.Add(_maxSyncWait))
            {
                // If we got a response, send it up
                var pong = GetCachedResponse<Pong>(ping.ConversationId);
                if (pong != null)
                {
                    return true; // They replied in time
                }

                Thread.Sleep(CheckInterval); // Otherwise wait for one
            }

            Log("Ping timeout");
            return false; // Nothing in time
        }

        #endregion

        #region Protocol Events

        /// <summary>
        /// Record every contact we see in our cache, if applicable. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        private void HandleMessage(Contact sender, Message message)
        {
            Log($"got {message.GetName()} from {message.SenderId}");
            SawContact(sender);
        }

        /// <summary>
        /// Store responses in the response cache to be picked up by threads waiting for them
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="response"></param>
        private void CacheResponse(Contact sender, Response response)
        {
            var entry = new CachedResponse {Arrived = DateTime.Now, Response = response};
            lock (_responseCache)
            {
                _responseCache[response.ConversationId] = entry;
            }
        }

        /// <summary>
        /// Get a properly typed response from the cache, or null if none exists.
        /// </summary>
        /// <param name="conversationId"></param>
        /// <returns></returns>
        private T GetCachedResponse<T>(Id conversationId) where T : Response
        {
            lock (_responseCache)
            {
                if (_responseCache.ContainsKey(conversationId))
                {
                    // If we found something of the right type
                    if (_responseCache[conversationId].Response is T result)
                    {
                        _responseCache.Remove(conversationId);
                        return result;
                    }
                }

                return null; // Nothing there -> null
            }
        }

        /// <summary>
        /// Respond to a ping by sending a pong
        /// </summary>
        private void HandlePing(Contact sender, Ping ping)
        {
            var pong = new Pong(NodeId, ping);

            SendMessage(sender.NodeEndPoint, pong);
        }

        /// <summary>
        /// Send back the contacts for the K closest nodes to the desired Id, not including the requester.
        /// K = BucketList.BUCKET_SIZE;
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="request"></param>
        private void HandleFindNode(Contact sender, FindNode request)
        {
            List<Contact> closeNodes = _buckets.CloseContacts(request.Target, sender.NodeId);
            var response = new FindNodeResponse(NodeId, request, closeNodes);
            SendMessage(sender.NodeEndPoint, response);
        }

        /// <summary>
        /// Give the value if we have it, or the closest nodes if we don't.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="request"></param>
        private void HandleFindValue(Contact sender, FindValue request)
        {
            if (_storage.ContainsKey(request.Key))
            {
                var values = _storage.Get(request.Key);
                var response = new FindValueDataResponse(NodeId, request, values);
                SendMessage(sender.NodeEndPoint, response);
            }
            else
            {
                List<Contact> closeNodes = _buckets.CloseContacts(request.Key, sender.NodeId);
                var response = new FindValueContactResponse(NodeId, request, closeNodes);
                SendMessage(sender.NodeEndPoint, response);
            }
        }

        /// <summary>
        /// Ask for data if we don't already have it. Update time info if we do.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="request"></param>
        private void HandleStoreQuery(Contact sender, StoreQuery request)
        {
            //TODO: check size and local storage policy
            Id key = request.Key;
            Id valHash = request.DataHash;

            if (!_storage.Contains(key, valHash))
            {
                _acceptedStoreRequests[request.ConversationId] = DateTime.Now; // Record that we accepted it
                SendMessage(sender.NodeEndPoint, new StoreResponse(NodeId, request, true));
            }
            else if (request.GetPublicationTimeUtc() > _storage.GetPublicationTime(key, valHash)
                     && request.GetPublicationTimeUtc() < DateTime.Now.ToUniversalTime().Add(_maxClockSkew))
            {
                // Update our recorded publication time
                _storage.Restamp(key, valHash, request.GetPublicationTimeUtc(), ExpireTime);
            }
        }

        /// <summary>
        /// Store the data, if we requested it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="request"></param>
        private void HandleStoreData(Contact sender, StoreData request)
        {
            // If we asked for it, store it and clear the authorization.
            lock (_acceptedStoreRequests)
            {
                if (_acceptedStoreRequests.ContainsKey(request.ConversationId))
                {
                    _acceptedStoreRequests.Remove(request.Key);

                    // TODO: Calculate when we should expire this data according to Kademlia
                    // For now just keep it until it expires

                    // Don't accept stuff published far in the future
                    if (request.GetPublicationTimeUtc() < DateTime.Now.ToUniversalTime().Add(_maxClockSkew))
                    {
                        // We re-hash since we shouldn't trust their hash
                        _storage.Put(request.Key, Id.Hash(request.Data), request.Data,
                            request.GetPublicationTimeUtc(), ExpireTime);
                    }
                }
            }
        }

        /// <summary>
        /// Send data in response to affirmative SendResponses
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="response"></param>
        private void HandleStoreResponse(Contact sender, StoreResponse response)
        {
            lock (_sentStoreRequests)
            {
                if (response.ShouldSendData
                    && _sentStoreRequests.ContainsKey(response.ConversationId))
                {
                    // We actually sent this
                    // Send along the data and remove it from the list
                    var toStore = _sentStoreRequests[response.ConversationId];
                    SendMessage(sender.NodeEndPoint,
                        new StoreData(NodeId, response, toStore.Key, Id.Hash(toStore.Val), toStore.Val,
                            toStore.Publication));
                    _sentStoreRequests.Remove(response.ConversationId);
                }
            }
        }

        /// <summary>
        /// Expire entries in the accepted/sent store request caches and the response cache.
        /// </summary>
        private void MindCaches()
        {
            while (_threadsRun)
            {
                // Do accepted requests
                lock (_acceptedStoreRequests)
                {
                    for (int i = 0; i < _acceptedStoreRequests.Count; i++)
                    {
                        // Remove stuff that is too old
                        if (DateTime.Now.Subtract(_acceptedStoreRequests.Values[i]) > MaxCacheTime)
                        {
                            _acceptedStoreRequests.RemoveAt(i);
                            i--;
                        }
                    }
                }

                // Do sent requests
                lock (_sentStoreRequests)
                {
                    for (int i = 0; i < _sentStoreRequests.Count; i++)
                    {
                        if (DateTime.Now.Subtract(_sentStoreRequests.Values[i].Sent) > MaxCacheTime)
                        {
                            _sentStoreRequests.RemoveAt(i);
                            i--;
                        }
                    }
                }

                // Do responses
                lock (_responseCache)
                {
                    for (int i = 0; i < _responseCache.Count; i++)
                    {
                        if (DateTime.Now.Subtract(_responseCache.Values[i].Arrived) > MaxCacheTime)
                        {
                            _responseCache.RemoveAt(i);
                            i--;
                        }
                    }
                }

                Thread.Sleep(CheckInterval);
            }
        }

        #endregion

        #region Framework

        /// <summary>
        /// Handle incoming packets
        /// </summary>
        private void MindUdpClient()
        {
            while (_threadsRun)
            {
                try
                {
                    // Get a datagram
                    var sender = new IPEndPoint(IPAddress.Any, 0); // Get from anyone
                    var data = _udpClient.Receive(ref sender);

                    // Decode the message
                    var memoryStream = new MemoryStream(data);
                    var binaryFormatter = new BinaryFormatter();
                    object incomingMessage = null;
                    try
                    {
                        incomingMessage = binaryFormatter.Deserialize(memoryStream);
                    }
                    catch (Exception)
                    {
                        Log("Invalid datagram!");
                    }

                    // Process the message
                    if (incomingMessage != null && incomingMessage is Message msg)
                    {
                        DispatchMessageEvents(sender, msg);
                    }
                    else
                    {
                        Log("Non-message object!");
                    }
                }
                catch (Exception ex)
                {
                    Log("Error receiving data: " + ex);
                }
            }
        }

        /// <summary>
        /// Sends out message events for the given message.
        /// ADD NEW MESSAGE TYPES HERE
        /// </summary>
        /// <param name="receivedFrom"></param>
        /// <param name="message"></param>
        private void DispatchMessageEvents(IPEndPoint receivedFrom, Message message)
        {
            // Make a contact for the person who sent it.
            Contact sender = new Contact(message.SenderId, receivedFrom);

            // Every message gets this one
            GotMessage?.Invoke(sender, message);

            // All responses get this one
            if (message is Response response)
                GotResponse?.Invoke(sender, response);

            switch (message)
            {
                case Ping ping:
                    GotPing?.Invoke(sender, ping);
                    break;
                case Pong pong:
                    GotPong?.Invoke(sender, pong);
                    break;
                case FindNode findNode:
                    GotFindNode?.Invoke(sender, findNode);
                    break;
                case FindNodeResponse findNodeResponse:
                    GotFindNodeResponse?.Invoke(sender, findNodeResponse);
                    break;
                case FindValue findValue:
                    GotFindValue?.Invoke(sender, findValue);
                    break;
                case FindValueContactResponse findValueContactResponse:
                    GotFindValueContactResponse?.Invoke(sender, findValueContactResponse);
                    break;
                case FindValueDataResponse findValueDataResponse:
                    GotFindValueDataResponse?.Invoke(sender, findValueDataResponse);
                    break;
                case StoreQuery storeQuery:
                    GotStoreQuery?.Invoke(sender, storeQuery);
                    break;
                case StoreResponse storeResponse:
                    GotStoreResponse?.Invoke(sender, storeResponse);
                    break;
                case StoreData storeData:
                    GotStoreData?.Invoke(sender, storeData);
                    break;
            }
        }

        /// <summary>
        /// Send a message to someone.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="message"></param>
        private void SendMessage(IPEndPoint destination, Message message)
        {
            // Encode the message
            var ms = new MemoryStream();
            IFormatter encoder = new BinaryFormatter();
            encoder.Serialize(ms, message);
            byte[] messageData = ms.GetBuffer();

            Log($"sending {message.GetName()} to {destination}");

            _udpClient.Send(messageData, messageData.Length, destination);
        }

        /// <summary>
        /// Call this whenever we see a contact.
        /// We add the contact to the queue to be cached.
        /// </summary>
        /// <param name="contact"></param>
        private void SawContact(Contact contact)
        {
            if (contact.NodeId == NodeId)
            {
                return; // NEVER insert ourselves!
            }

            if (_contactQueue.Count < MaxQueueLength)
            {
                _contactQueue.Enqueue(contact);
            }
        }

        /// <summary>
        /// Run in the background and add contacts to the cache.
        /// </summary>
        private void MindBuckets()
        {
            while (_threadsRun)
            {
                // Handle all the queued contacts
                while (!_contactQueue.IsEmpty)
                {
                    // Only lock when getting stuff.
                    if (!_contactQueue.TryDequeue(out var applicant))
                    {
                        Thread.Sleep(CheckInterval);
                        continue;
                    }

                    Log("Processing contact for " + applicant.NodeId);

                    // If we already know about them
                    if (_buckets.Contains(applicant.NodeId))
                    {
                        // If they have a new address, record that
                        if (!Equals(_buckets.Get(applicant.NodeId).NodeEndPoint, applicant.NodeEndPoint))
                        {
                            // Replace old one
                            _buckets.Remove(applicant.NodeId);
                            _buckets.Put(applicant);
                        }
                        else
                        {
                            // Just promote them
                            _buckets.Promote(applicant.NodeId);
                        }

                        continue; // next in queue
                    }

                    // If we can fit them, do so
                    var blocker = _buckets.Blocker(applicant.NodeId);
                    if (blocker == null)
                    {
                        _buckets.Put(applicant);
                    }
                    else
                    {
                        // We can't fit them. We have to choose between blocker and applicant
                        if (!SyncPing(blocker.NodeEndPoint))
                        {
                            // If the blocker doesn't respond, pick the applicant.
                            _buckets.Remove(blocker.NodeId);
                            _buckets.Put(applicant);
                            Log("Chose applicant");
                        }
                        else
                        {
                            Log("Chose blocker");
                        }
                    }
                }

                // Wait for more
                Thread.Sleep(CheckInterval);
            }
        }

        /// <summary>
        /// Log debug messages, if debugging is enabled.
        /// </summary>
        /// <param name="message"></param>
        private void Log(string message)
        {
            if (_debug)
                Console.WriteLine($"{NodeId} {message}");
        }

        #endregion
    }
}