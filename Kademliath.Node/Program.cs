using System;
using System.Linq;
using Core;

namespace Kademliath.Node
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("-master"))
            {
                var node = new KademliaNode(8810, Id.RandomId());
                node.EnableDebug();
                node.JoinNetwork(); // Try to join network. Fail.
            }
            
            Console.WriteLine("Initializing Dummy Node...");
			
            var dht = new Dht("http://localhost/daylight/", false);


			
            // Sleep until we're killed.
            while(true) {
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}