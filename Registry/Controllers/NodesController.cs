using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Registry.Models;

namespace Registry.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class NodesController : ControllerBase
    {
        private readonly IMemoryCache _memoryCache;
        private const string NodeListKey = "NODELISTKEY";
        private readonly List<Node> _nodeList;

        public NodesController(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;

            _nodeList = _memoryCache.GetOrCreate(NodeListKey,
                c => new List<Node> {new Node {HostAddress = "127.0.0.1", HostPort = 8810}});
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(_nodeList);
        }


        [HttpPost]
        public void Post([FromBody] Node newNode)
        {
            newNode.HostAddress = HttpContext.Connection.RemoteIpAddress.ToString();
            if (!_nodeList.Any(n => n.HostAddress == newNode.HostAddress && n.HostPort == newNode.HostPort))
            {
                _nodeList.Add(newNode);
            }

            // keep at most 10
            if (_nodeList.Count > 10)
            {
                _nodeList.RemoveAt(1);
            }

            _memoryCache.Set(NodeListKey, _nodeList);
        }
    }
}