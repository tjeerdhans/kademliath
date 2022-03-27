using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using Kademliath.Core;

namespace Kademliath.Node
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && (args[0] == "-m" || args[0] == "--master"))
            {
                Console.WriteLine($"Initializing master node..");
                int port = 8810;
                if (args.Length >= 2)
                {
                    if (!int.TryParse(args[1], out port))
                    {
                        port = 8810;
                    }
                }

                var node = new KademliaNode(port, new Id()) { Debug = true };
                node.JoinNetwork(); // Try to join network. Fail because we are the master node.
            }
            else
            {
                Console.WriteLine($"Initializing a node..");

                var dht1 = new Dht("http://localhost:5000/", true) { Debug = true };

                if (args.Contains("--seed") || args.Contains("-s"))
                {
                    // store some data
                    var data = new Dictionary<string, byte[]>();
                    for (int i = 0; i < 1; i++)
                    {
                        var key = Guid.NewGuid().ToString();
                        var value = Guid.NewGuid().ToByteArray();
                        data.Add(key, value);
                        dht1.Put(key, value);
                    }
                }
            }

            // Sleep until we're killed.
            while (true)
            {
                Thread.Sleep(1000);
            }
        }
    }
}