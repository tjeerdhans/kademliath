using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Kademliath.Core;

namespace Kademliath.Node
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("-master"))
            {
                var node = new KademliaNode(8810, new Id());
                node.EnableDebug();
                node.JoinNetwork(); // Try to join network. Fail.
            }

            var nodeCount = 1;
            Console.WriteLine($"Initializing {nodeCount} nodes..");
            var nodes = new List<Dht>();

            for (int i = 0; i < nodeCount; i++)
            {
                var dht = new Dht("http://localhost:5000/", true);
                dht.EnableDebug();
                dht.Put("programmer","tjeerdhans");
                nodes.Add(dht);
            }
			
            
            // Sleep until we're killed.
            while(true) {
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}