using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Core.Messages;

namespace Core
{
    /// <summary>
    /// Functions as a peer in the overlay network.
    /// </summary>
    public class KademliaNode : IDisposable
    {
        // Identity
        private readonly Id _nodeId;

        // Network State
        private readonly BucketList _contactCache;
        private readonly Thread _bucketMinder; // Handle updates to cache
        private const int CheckInterval = 1;
        private readonly List<Contact> _contactQueue; // Add contacts here to be considered for caching
        private const int MaxQueueLength = 10;

        // Response cache
        // We want to be able to synchronously wait for responses, so we have other threads put them in this cache.
        // We also need to discard old ones.
        private struct CachedResponse
        {
            public Response Response;
            public DateTime Arrived;
        }

        private readonly SortedList<Id, CachedResponse> _responseCache;
        private readonly TimeSpan _maxSyncWait = new TimeSpan(5000000); // 500 ms in ticks

        // Application (datastore)
        private readonly LocalStorage _datastore; // Keep our key/value pairs

        private readonly SortedList<Id, DateTime>
            _acceptedStoreRequests; // Store a list of what put requests we actually accepted while waiting for data.

        // The list of put requests we sent is more complex
        // We need to keep the data and timestamp, but don't want to insert it in our storage.
        // So we keep it in a cache, and discard it if it gets too old.
        private struct OutstandingStoreRequest
        {
            public Id Key;
            public object Val;
            public DateTime Publication;
            public DateTime Sent;
        }

        private readonly SortedList<Id, OutstandingStoreRequest> _sentStoreRequests;

        // We need a thread to go through and expire all these things
        private readonly Thread _authMinder;
        private static readonly TimeSpan MaxCacheTime = new TimeSpan(0, 0, 30);

        // How much clock skew do we tolerate?
        private readonly TimeSpan _maxClockSkew = new TimeSpan(1, 0, 0);

        // Kademlia config
        private const int Parallellism = 3; // Number of requests to run at once for iterative operations.
        private const int NodesToFind = 20; // = k = bucket size

        private static readonly TimeSpan
            ExpireTime = new TimeSpan(24, 0, 0); // Time since original publication to expire a value

        private static readonly TimeSpan
            RefreshTime = new TimeSpan(1, 0, 0); // Time since last bucket access to refresh a bucket

        private static readonly TimeSpan
            ReplicateTime = new TimeSpan(1, 0, 0); // Interval at which we should re-insert our whole database

        private DateTime _lastReplication;
        //private static TimeSpan _republishTime = new TimeSpan(23, 0, 0); // Interval at which we should re-insert our values with new publication times

        // How often do we run high-level maintainance (expiration, etc.)
        private static readonly TimeSpan MaintenanceInterval = new TimeSpan(0, 10, 0);
        private readonly Thread _maintenanceMinder;

        // Network IO
        private readonly UdpClient _udpClient;

        //private Socket client;
        private readonly Thread _clientMinder;

        // Events
        // Messages are strongly typed. Hooray!
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
            _bucketMinder.Join();
            _authMinder.Join();
            _clientMinder.Join();
            _maintenanceMinder.Join();
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
            _nodeId = id;
            _contactCache = new BucketList(_nodeId);
            _contactQueue = new List<Contact>();
            _datastore = new LocalStorage();
            _acceptedStoreRequests = new SortedList<Id, DateTime>();
            _sentStoreRequests = new SortedList<Id, OutstandingStoreRequest>();
            _responseCache = new SortedList<Id, CachedResponse>();
            _lastReplication = default(DateTime);

            // Start minding the buckets
            _bucketMinder = new Thread(MindBuckets) {IsBackground = true};
            _bucketMinder.Start();

            // Start minding the conversation state caches
            _authMinder = new Thread(MindCaches) {IsBackground = true};
            _authMinder.Start();

            // Set all the event handlers
            GotMessage += HandleMessage;
            GotResponse += CacheResponse;

            GotPing += HandlePing;
            GotFindNode += HandleFindNode;
            GotFindValue += HandleFindValue;
            GotStoreQuery += HandleStoreQuery;
            GotStoreResponse += HandleStoreResponse;
            GotStoreData += HandleStoreData;

            // Connect
            _udpClient = new UdpClient();

            // create a socket that listens on all addresses (IPv4 _and_ IPv6)
            // http://blogs.msdn.com/b/malarch/archive/2005/11/18/494769.aspx
            Socket sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            sock.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName) 27, 0);
            sock.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
            _udpClient.Client = sock;

            _clientMinder = new Thread(MindClient);
            _clientMinder.IsBackground = true;
            _clientMinder.Start();

            // Start maintainance
            _maintenanceMinder = new Thread(MindMaintainance);
            _maintenanceMinder.IsBackground = true;
            _maintenanceMinder.Start();
        }

        /// <summary>
        /// Bootstrap by pinging a local node on the specified port.
        /// </summary>
        /// <param name="localPort"></param>
        /// <returns></returns>
        public bool Bootstrap(int localPort)
        {
            return Bootstrap(new IPEndPoint(IPAddress.Loopback, localPort));
        }

        /// <summary>
        /// Bootstrap the node by having it ping another node. Returns true if we get a response.
        /// </summary>
        /// <param name="other"></param>
        /// <returns>True if bootstrapping was successful.</returns>
        public bool Bootstrap(IPEndPoint other)
        {
            // Send a blocking ping.
            bool worked = SyncPing(other);
            Thread.Sleep(CheckInterval); // Wait for them to notice us
            return worked;
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
            IList<Contact> found = IterativeFindNode(_nodeId);
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
            // RefreshBuckets(); // Put this off until first maintainance.
            if (_contactCache.GetCount() > 0)
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
        /// Enables degugging output for the node.
        /// </summary>
        public void EnableDebug()
        {
            _debug = true;
        }

        /// <summary>
        /// Returns the Id of the node
        /// </summary>
        /// <returns></returns>
        public Id GetId()
        {
            return _nodeId;
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
            if (_datastore.ContainsKey(key))
            {
                // Check the local datastore first.
                return _datastore.Get(key);
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
        private void MindMaintainance()
        {
            while (_threadsRun)
            {
                Thread.Sleep(MaintenanceInterval);
                Log("Performing maintenance");
                // Expire old
                _datastore.Expire();
                Log(_datastore.GetKeys().Count + " keys stored.");

                // Replicate all if needed
                // We get our own lists to iterate
                if (DateTime.Now > _lastReplication.Add(ReplicateTime))
                {
                    Log("Replicating");
                    foreach (Id key in _datastore.GetKeys())
                    {
                        foreach (Id valHash in _datastore.GetHashes(key))
                        {
                            try
                            {
                                // ReSharper disable once PossibleInvalidOperationException
                                IterativeStore(key, _datastore.Get(key, valHash),
                                    (DateTime) _datastore.GetPublicationTime(key, valHash));
                            }
                            catch (Exception ex)
                            {
                                Log("Could not replicate " + key + "/" + valHash + ": " + ex);
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
            IList<Id> toLookup = _contactCache.IdsForRefresh(RefreshTime);
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
                SyncStore(c.GetEndPoint(), key, val, originalInsertion);
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
            IList<object> found;
            close = IterativeFind(target, true, out found);
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
            if (target != _nodeId)
            {
                _contactCache.Touch(target);
            }

            // Get the alpha closest nodes to the target
            // TODO: Should actually pick from a certain bucket.
            SortedList<Id, Contact> shortlist = new SortedList<Id, Contact>();
            foreach (Contact c in _contactCache.CloseContacts(Parallellism, target, null))
            {
                shortlist.Add(c.GetId(), c);
            }

            int shortlistIndex = 0; // Everyone before this is up.

            // Make an initial guess for the closest node
            Contact closest = null;
            foreach (Contact toAsk in shortlist.Values)
            {
                if (closest == null || (toAsk.GetId() ^ target) < (closest.GetId() ^ target))
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
                for (int i = shortlistIndex; i < shortlistIndex + Parallellism && i < shortlist.Count; i++)
                {
                    List<Contact> suggested;
                    if (getValue)
                    {
                        // Get list or value
                        suggested = SyncFindValue(shortlist.Values[i].GetEndPoint(), target, out var returnedValues);
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
                        suggested = SyncFindNode(shortlist.Values[i].GetEndPoint(), target);
                    }

                    if (suggested != null)
                    {
                        // Add suggestions to shortlist and check for closest
                        foreach (Contact suggestion in suggested)
                        {
                            if (!shortlist.ContainsKey(suggestion.GetId()))
                            {
                                // Contacts aren't value types so we have to do this.
                                shortlist.Add(suggestion.GetId(), suggestion);
                            }

                            if (closest != null && (suggestion.GetId() ^ target) < (closest.GetId() ^ target))
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

                shortlistIndex += Parallellism;
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
            Message storeIt = new StoreQuery(_nodeId, key, Id.Hash(val), originalInsertion, val.ToByteArray().Length);

            // Record having sent it
            OutstandingStoreRequest req = new OutstandingStoreRequest
            {
                Key = key, Val = val, Sent = DateTime.Now, Publication = originalInsertion
            };
            lock (_sentStoreRequests)
            {
                _sentStoreRequests[storeIt.GetConversationId()] = req;
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
            Message question = new FindNode(_nodeId, toFind);
            SendMessage(ask, question);

            while (DateTime.Now < called.Add(_maxSyncWait))
            {
                // If we got a response, send it up
                FindNodeResponse resp = GetCachedResponse<FindNodeResponse>(question.GetConversationId());
                if (resp != null)
                {
                    return resp.GetContacts();
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
            DateTime called = DateTime.Now;
            Message question = new FindValue(_nodeId, toFind);
            SendMessage(ask, question);

            while (DateTime.Now < called.Add(_maxSyncWait))
            {
                // See if we got data!
                FindValueDataResponse dataResp = GetCachedResponse<FindValueDataResponse>(question.GetConversationId());
                if (dataResp != null)
                {
                    // Send it out and return null!
                    val = dataResp.GetValues();
                    return null;
                }

                // If we got a contact, send it up
                FindValueContactResponse resp =
                    GetCachedResponse<FindValueContactResponse>(question.GetConversationId());
                if (resp != null)
                {
                    val = null;
                    return resp.GetContacts();
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
        /// <param name="toPing"></param>
        /// <returns>true on a response, false otherwise</returns>
        private bool SyncPing(IPEndPoint toPing)
        {
            // Send message
            DateTime called = DateTime.Now;
            Message ping = new Ping(_nodeId);
            SendMessage(toPing, ping);

            while (DateTime.Now < called.Add(_maxSyncWait))
            {
                // If we got a response, send it up
                Pong resp = GetCachedResponse<Pong>(ping.GetConversationId());
                if (resp != null)
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
        /// <param name="msg"></param>
        private void HandleMessage(Contact sender, Message msg)
        {
            Log(_nodeId + " got " + msg.GetName() + " from " + msg.GetSenderId());
            SawContact(sender);
        }

        /// <summary>
        /// Store responses in the response cache to be picked up by threads waiting for them
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="response"></param>
        private void CacheResponse(Contact sender, Response response)
        {
            CachedResponse entry = new CachedResponse {Arrived = DateTime.Now, Response = response};
            //Log("Caching " + response.GetName() + " under " + response.GetConversationID().ToString());
            // Store in cache
            lock (_responseCache)
            {
                _responseCache[response.GetConversationId()] = entry;
            }
        }

        /// <summary>
        /// Get a properly typed response from the cache, or null if none exists.
        /// </summary>
        /// <param name="conversation"></param>
        /// <returns></returns>
        private T GetCachedResponse<T>(Id conversation) where T : Response
        {
            lock (_responseCache)
            {
                if (_responseCache.ContainsKey(conversation))
                {
                    // If we found something of the right type
                    try
                    {
                        T toReturn = (T) _responseCache[conversation].Response;
                        _responseCache.Remove(conversation);
                        //Log("Retrieving cached " + toReturn.GetName());
                        return toReturn; // Pull it out and return it
                    }
                    catch (Exception)
                    {
                        // Couldn't actually cast to type we want.
                        return null;
                    }
                }
                else
                {
                    //Log("Nothing for " + conversation.ToString());
                    return null; // Nothing there -> null
                }
            }
        }

        /// <summary>
        /// Respond to a ping by sending a pong
        /// </summary>
        private void HandlePing(Contact sender, Ping ping)
        {
            Message pong = new Pong(_nodeId, ping);

            SendMessage(sender.GetEndPoint(), pong);
        }

        /// <summary>
        /// Send back the contacts for the K closest nodes to the desired Id, not including the requester.
        /// K = BucketList.BUCKET_SIZE;
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="request"></param>
        private void HandleFindNode(Contact sender, FindNode request)
        {
            List<Contact> closeNodes = _contactCache.CloseContacts(request.GetTarget(), sender.GetId());
            Message response = new FindNodeResponse(_nodeId, request, closeNodes);
            SendMessage(sender.GetEndPoint(), response);
        }

        /// <summary>
        /// Give the value if we have it, or the closest nodes if we don't.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="request"></param>
        private void HandleFindValue(Contact sender, FindValue request)
        {
            if (_datastore.ContainsKey(request.GetKey()))
            {
                IList<object> vals = _datastore.Get(request.GetKey());
                Message response = new FindValueDataResponse(_nodeId, request, vals);
                SendMessage(sender.GetEndPoint(), response);
            }
            else
            {
                List<Contact> closeNodes = _contactCache.CloseContacts(request.GetKey(), sender.GetId());
                Message response = new FindValueContactResponse(_nodeId, request, closeNodes);
                SendMessage(sender.GetEndPoint(), response);
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
            Id key = request.GetKey();
            Id valHash = request.GetDataHash();

            if (!_datastore.Contains(key, valHash))
            {
                _acceptedStoreRequests[request.GetConversationId()] = DateTime.Now; // Record that we accepted it
                SendMessage(sender.GetEndPoint(), new StoreResponse(_nodeId, request, true));
            }
            else if (request.GetPublicationTime() > _datastore.GetPublicationTime(key, valHash)
                     && request.GetPublicationTime() < DateTime.Now.ToUniversalTime().Add(_maxClockSkew))
            {
                // Update our recorded publicaton time
                _datastore.Restamp(key, valHash, request.GetPublicationTime(), ExpireTime);
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
                if (_acceptedStoreRequests.ContainsKey(request.GetConversationId()))
                {
                    _acceptedStoreRequests.Remove(request.GetKey());

                    // TODO: Calculate when we should expire this data according to Kademlia
                    // For now just keep it until it expires

                    // Don't accept stuff published far in the future
                    if (request.GetPublicationTime() < DateTime.Now.ToUniversalTime().Add(_maxClockSkew))
                    {
                        // We re-hash since we shouldn't trust their hash
                        _datastore.Put(request.GetKey(), Id.Hash(request.GetData()), request.GetData(),
                            request.GetPublicationTime(), ExpireTime);
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
                if (response.ShouldSendData()
                    && _sentStoreRequests.ContainsKey(response.GetConversationId()))
                {
                    // We actually sent this
                    // Send along the data and remove it from the list
                    OutstandingStoreRequest toStore = _sentStoreRequests[response.GetConversationId()];
                    SendMessage(sender.GetEndPoint(),
                        new StoreData(_nodeId, response, toStore.Key, Id.Hash(toStore.Val), toStore.Val,
                            toStore.Publication));
                    _sentStoreRequests.Remove(response.GetConversationId());
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
        private void MindClient()
        {
            while (_threadsRun)
            {
                try
                {
                    // Get a datagram
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0); // Get from anyone
                    byte[] data = _udpClient.Receive(ref sender);

                    // Decode the message
                    MemoryStream ms = new MemoryStream(data);
                    IFormatter decoder = new BinaryFormatter();
                    object got = null;
                    try
                    {
                        got = decoder.Deserialize(ms);
                    }
                    catch (Exception)
                    {
                        Log("Invalid datagram!");
                    }

                    // Process the message
                    if (got != null && got is Message msg)
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
        /// <param name="msg"></param>
        private void DispatchMessageEvents(IPEndPoint receivedFrom, Message msg)
        {
            // Make a contact for the person who sent it.
            Contact sender = new Contact(msg.GetSenderId(), receivedFrom);

            // Every message gets this one
            GotMessage?.Invoke(sender, msg);

            // All responses get this one
            if (msg is Response response)
                GotResponse?.Invoke(sender, response);

            switch (msg)
            {
                // All messages have special events
                // TODO: Dynamically register from each message class instead of this ugly elsif?
                case Ping ping:
                    // Pings
                    GotPing?.Invoke(sender, ping);
                    break;
                case Pong pong:
                    // Pongs
                    GotPong?.Invoke(sender, pong);
                    break;
                case FindNode node:
                    // Node search
                    GotFindNode?.Invoke(sender, node);
                    break;
                case FindNodeResponse message:
                    GotFindNodeResponse?.Invoke(sender, message);
                    break;
                case FindValue value:
                    // Key search
                    GotFindValue?.Invoke(sender, value);
                    break;
                case FindValueContactResponse message:
                    GotFindValueContactResponse?.Invoke(sender, message);
                    break;
                case FindValueDataResponse message:
                    GotFindValueDataResponse?.Invoke(sender, message);
                    break;
                case StoreQuery query:
                    GotStoreQuery?.Invoke(sender, query);
                    break;
                case StoreResponse message:
                    GotStoreResponse?.Invoke(sender, message);
                    break;
                case StoreData data:
                    GotStoreData?.Invoke(sender, data);
                    break;
            }
        }

        /// <summary>
        /// Send a mesaage to someone.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="msg"></param>
        private void SendMessage(IPEndPoint destination, Message msg)
        {
            // Encode the message
            var ms = new MemoryStream();
            IFormatter encoder = new BinaryFormatter();
            encoder.Serialize(ms, msg);
            byte[] messageData = ms.GetBuffer();

            Log(_nodeId + " sending " + msg.GetName() + " to " + destination);

            _udpClient.Send(messageData, messageData.Length, destination);
        }

        /// <summary>
        /// Call this whenever we see a contact.
        /// We add the contact to the queue to be cached.
        /// </summary>
        /// <param name="seen"></param>
        private void SawContact(Contact seen)
        {
            if (seen.GetId() == _nodeId)
            {
                return; // NEVER insert ourselves!
            }

            lock (_contactQueue)
            {
                if (_contactQueue.Count < MaxQueueLength)
                {
                    // Don't let it get too long
                    _contactQueue.Add(seen);
                }
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
                while (_contactQueue.Count > 0)
                {
                    Contact applicant;
                    lock (_contactQueue)
                    {
                        // Only lock when getting stuff.
                        applicant = _contactQueue[0];
                        _contactQueue.RemoveAt(0);
                    }

                    //Log("Processing contact for " + applicant.GetID().ToString());

                    // If we already know about them
                    if (_contactCache.Contains(applicant.GetId()))
                    {
                        // If they have a new address, record that
                        if (!Equals(_contactCache.Get(applicant.GetId()).GetEndPoint(), applicant.GetEndPoint()))
                        {
                            // Replace old one
                            _contactCache.Remove(applicant.GetId());
                            _contactCache.Put(applicant);
                        }
                        else
                        {
                            // Just promote them
                            _contactCache.Promote(applicant.GetId());
                        }

                        continue;
                    }

                    // If we can fit them, do so
                    Contact blocker = _contactCache.Blocker(applicant.GetId());
                    if (blocker == null)
                    {
                        _contactCache.Put(applicant);
                    }
                    else
                    {
                        // We can't fit them. We have to choose between blocker and applicant
                        if (!SyncPing(blocker.GetEndPoint()))
                        {
                            // If the blocker doesn't respond, pick the applicant.
                            _contactCache.Remove(blocker.GetId());
                            _contactCache.Put(applicant);
                            Log("Chose applicant");
                        }
                        else
                        {
                            Log("Chose blocker");
                        }
                    }

                    //Log(contactCache.ToString());
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
                Console.WriteLine(message);
        }

        #endregion
    }
}