# kademliath

An implementation of the Kademlia DHT (distributed hash table) protocol in C#. 

## Components

 - Core - the protocol
 - Node - a console app that starts a node and optionally a master node (command line arg '-master')
 - Registry - web api that provides and saves node addresses in it's memorycache.
 
## Getting started

1. Start an instance of the Registry (starts at https://localhost:5001/nodes and http://localhost:5000/nodes)
2. Run a node
3. Run more nodes
4. Profit

## Attribution and license
I found the code on which I based this more than five years ago. The name of the project was 'Daylight' (I think).
Unfortunately, the code and author (someone called 'anovak') have disappeared since then (only from the internet, I hope).

Since then, I've changed and refactored the code extensively, and in the process I've removed the 
following header that appeared in every file:
 ```
 /*
 * Created by SharpDevelop.
 * User: anovak
 * Date: 6/22/2010
 * Time: 7:13 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
 ```
 
So, if you're anovak and recognize this code, please contact me (preferably, add an issue here), 
so I can reinstate proper attribution and possibly a license.

