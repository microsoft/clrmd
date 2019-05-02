using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Diagnostics.Runtime;

namespace ContainerWebApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MemDiagController : ControllerBase
    {
        [HttpGet]
        public object Get()
        {
            GC.Collect();

            using(DataTarget target = this.CreateDataTargetFromSelf())
            {
                var clrInfo = target.ClrVersions.First();
                var runtime = clrInfo.CreateRuntime();
                var heap = runtime.Heap;
                var result = new {
                    TotalHeapSize = heap.TotalHeapSize,
                    PointerSize = heap.PointerSize,
                    SegmentCount = heap.Segments.Count
                };
                kill(target.ProcessId, 9); // kill the target process
                waitpid(target.ProcessId, IntPtr.Zero, 0);
                return result;
            }
        }

        [HttpGet("details/{count}")]
        public object GetDetails(int count)
        {
            using(DataTarget target = this.CreateDataTargetFromSelf())
            {
                var clrInfo = target.ClrVersions.First();
                var runtime = clrInfo.CreateRuntime();
                var heap = runtime.Heap;
                
                Dictionary<string, HeapTypeInfo> typeInfo = new Dictionary<string, HeapTypeInfo>();
                foreach (ClrObject obj in heap.EnumerateObjects())
                {
                    HeapTypeInfo hti;
                    if (!typeInfo.TryGetValue(obj.Type.Name, out hti))
                    {
                        hti = new HeapTypeInfo();
                        hti.TypeName = obj.Type.Name;
                        typeInfo.Add(obj.Type.Name, hti);
                    }
                    hti.TotalSize += obj.Size;
                    hti.ObjectCount++;
                }
                var orderByCountList = typeInfo.OrderByDescending(t => t.Value.ObjectCount).Take(count).Select(t => t.Value).ToArray();
                var orderBySizeList = typeInfo.OrderByDescending(t => t.Value.TotalSize).Take(count).Select(t => t.Value).ToArray();
                var result = new {
                    TopTypeByCount = orderByCountList,
                    TopTypeBySize = orderBySizeList
                };
                kill(target.ProcessId, 9); // kill the target process
                waitpid(target.ProcessId, IntPtr.Zero, 0);
                return result;
            }
        }

        private DataTarget CreateDataTargetFromSelf()
        {
            uint pid = fork();
            if (pid == 0)
            {
                // this is the child process. sleep for 5 minutes and terminate
                // in case parent process failed to kill it.
                System.Threading.Thread.Sleep(300000);
                Console.WriteLine($"process is going to Exit");
                //Environment.Exit(0);
            }
            Console.WriteLine($"process {pid} forked.");
            // parent process. suspect the child process by sending the SIGSTOP (19)
            int ret = kill(pid, 19);
            if (ret != 0)
            {
                throw new InvalidOperationException($"kill -stop {pid} failed with {ret}.");
            }
            Console.WriteLine($"process {pid} suspended");
            return DataTarget.AttachToProcess((int)pid, 0, AttachFlag.Passive);
        }

        [DllImport("libc", SetLastError = true)]
        private static extern uint fork();

        [DllImport("libc", SetLastError = true)]
        private static extern int kill(uint pid, int sig);


        [DllImport("libc", SetLastError = true)]
        private static extern uint waitpid(uint pid, IntPtr status, int options);
    }

    public class HeapTypeInfo
    {
        public ulong TotalSize { get; set; }

        public string TypeName { get; set; }

        public int ObjectCount { get; set; }
    }
}
