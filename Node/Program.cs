using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kademliath.Core;

namespace Kademliath.Node
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && (args[0]=="-m" || args[0]=="--master"))
            {
                Console.WriteLine($"Initializing master node..");
                int port;
                if (args.Length>=2)
                {
                    if (!int.TryParse(args[1], out port))
                    {
                        port = 8810;
                    }
                }
                
                var node = new KademliaNode(port, new Id());
                node.EnableDebug();
                node.JoinNetwork(); // Try to join network. Fail.
            }
            else
            {
                Console.WriteLine($"Initializing a node..");

                var dht1 = new Dht("http://localhost:5000/", true);
                dht1.EnableDebug();
                //var dht2 = new Dht("http://localhost:5000/", true);
                //dht2.EnableDebug();

                if (args.Contains("-seed"))
                {
                    // store some data
                    var data = new Dictionary<string, string>();
                    for (int i = 0; i < 10; i++)
                    {
                        var key = Guid.NewGuid().ToString();
                        var value = Guid.NewGuid().ToString();
                        data.Add(key, value);
                        dht1.Put(key, value);
                    }
                }
            }


//            Thread.Sleep(500);
//            var retrievedValue = dht2.Get("programmer");
//            Console.WriteLine($"Retrieved {retrievedValue} from {dht2.NodeId}");

            // Console.Read();

            // Sleep until we're killed.
            while (true)
            {
                Thread.Sleep(1000);
            }
        }
    }
}