using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ContainerWebApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LeakyController : ControllerBase
    {
        private static ConcurrentBag<MyDataType> _dataSet = new ConcurrentBag<MyDataType>();

        [HttpGet]
        public int Get()
        {
            return _dataSet.Count;
        }

        [HttpGet("leak")]
        public string DoLeak()
        {
            for(int i = 0; i < 10000; i++)
            {
                var d = new MyDataType(i, $"content {i}");
                _dataSet.Add(d);
            }
            return $"dataset holds {_dataSet.Count} items.";
        }
    }

    internal class MyDataType
    {
        public MyDataType()
        {
        }

        public MyDataType(int index, string content)
        {
            this.Index = index;
            this.Content = content;
        }

        public string Content { get; set; }
        public int Index { get; set; }
    }
}
