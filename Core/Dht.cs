using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Kademliath.Core;

namespace Core
{
    /// <summary>
    /// This is the class you use to use the library.
    /// You can put and get values.
    /// It is responsible for bootstrapping the local node and connecting to the appropriate overlay.
    /// It also registers us with the overlay.
    /// </summary>
    public class Dht
    {
        /// <summary>
        /// Returns the maximum size of individual puts.
        /// </summary>
        private const int MaxSize = 8 * 1024; // 8K is big

        private const string DefaultOverlayUrl = "http://localhost:5000/";
        private const string ListFragment = "nodes";
        private const string RegisterFragment = "nodes";

        private readonly KademliaNode _dhtNode;

        private bool _debug;

        /// <summary>
        /// Create a new DHT. It should connect to the default overlay network
        /// if possible, or use an existing connection, and do default things
        /// with regard to UPnP and storage on the local filesystem. It also
        /// will by default announce itself to the master list.
        /// </summary>
        public Dht()
            : this(DefaultOverlayUrl, true)
        {
        }

        /// <summary>
        /// Create a DHT using the given master server, and specify whether to publish our IP.
        /// PRECONDITION: Create one per app or you will have a node Id collision.
        /// TODO: Fix this.
        /// </summary>
        /// <param name="overlayUrl"></param>
        /// <param name="register"></param>
        public Dht(string overlayUrl, bool register)
        {
            // Make a new node and get port
            _dhtNode = new KademliaNode();
            var ourPort = _dhtNode.GetPort();
            Console.WriteLine("We are on UDP port " + ourPort);

            // Bootstrap with some nodes
            var downloader = new WebClient();
            downloader.Proxy = null; // TODO: Let client specify proxy
            //downloader.Proxy = new WebProxy("www-proxy.nl.int.atosorigin.com:8080", true);
            Console.WriteLine("Getting bootstrap list..");
            // TODO: Handle 404, etc.
            var nodeList = new List<Node>();
            try
            {
                using (var client = new HttpClient())
                {
                    var response = client.GetAsync(overlayUrl + ListFragment).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        var nodes = response.Content.ReadAsAsync<IEnumerable<Node>>().Result;
                        foreach (var node in nodes)
                        {
                            Console.WriteLine($"Got node {node.HostAddress}:{node.HostPort}");
                        }

                        nodeList.AddRange(nodes);
                    }
                }
            }
            catch (Exception)
            {
                nodeList.AddRange(new[] {new Node {HostAddress = "127.0.0.1", HostPort = 8810}});
            }

            foreach (var node in nodeList)
            {
                try
                {
                    IPEndPoint bootstrapNode = new IPEndPoint(IPAddress.Parse(node.HostAddress), node.HostPort);
                    Console.Write("Bootstrapping with " + bootstrapNode + ": ");
                    if (_dhtNode.Bootstrap(bootstrapNode))
                    {
                        Console.WriteLine("OK!");
                    }
                    else
                    {
                        Console.WriteLine("Failed.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Bad entry! Exception: " + ex.Message);
                }
            }

            // Join the network officially
            Console.WriteLine("Joining network...");
            if (_dhtNode.JoinNetwork())
            {
                Console.WriteLine("Joined successfully.");
                if (register)
                {
                    // Announce our presence
                    try
                    {
                        using (var client = new HttpClient())
                        {
                            var unused = client
                                .PostAsJsonAsync(overlayUrl + RegisterFragment, new Node {HostPort = ourPort}).Result;
                        }

                        Console.WriteLine("Announced presence.");
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Error while announcing presence: {exception.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine("Unable to connect to the overlay!\n"
                                  + "Check that the master server is returning accessible nodes.");
            }
        }

        /// <summary>
        /// Retrieve a value from the DHT.
        /// </summary>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <returns>an arbitrary value stored for the key, or null if no values are found</returns>
        public object Get(string key)
        {
            IList<object> found = _dhtNode.Get(Id.Hash(key));
            if (found.Count > 0)
            {
                return found[0]; // An arbitrary value
            }
            else
            {
                return null; // Nothing there
            }
        }

        /// <summary>
        /// Retrieve all applicable values from the DHT.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IList<object> GetAll(string key)
        {
            return _dhtNode.Get(Id.Hash(key));
        }

        /// <summary>
        /// Puts a value in the DHT under a key.
        /// </summary>
        /// <param name="key">Can be any length, is hashed internally.</param>
        /// <param name="val">Can be up to and including MaxSize() UTF-8 characters.</param>
        public void Put(string key, object val)
        {
            var keyHash = Id.Hash(key);
            Log($"Putting {key}({keyHash}):{val}");
            _dhtNode.Put(keyHash, val);
        }

        public void EnableDebug()
        {
            _debug = true;
            _dhtNode.EnableDebug();
        }

        /// <summary>
        /// Log debug messages, if debugging is enabled.
        /// </summary>
        /// <param name="message"></param>
        private void Log(string message)
        {
            if (_debug)
                Console.WriteLine($"{_dhtNode.NodeId} {message}");
        }
    }
}