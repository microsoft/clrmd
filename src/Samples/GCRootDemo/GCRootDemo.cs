using System;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Tests;

namespace GCRootDemo
{
    class Program
    {
        static void Main()
        {
            Helpers.TestWorkingDirectory = Environment.CurrentDirectory;

            // Please see src\Microsoft.Diagnostics.Runtime.Tests\Targets\GCRoot.cs for the debuggee we
            // are currently debugging.
            DataTarget dt = TestTargets.GCRoot.LoadFullDump();

            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            // This is the object we are looking for.  Let's say the address is 0x1234.  The following
            // demo will show you how to build output similar to SOS's "!gcroot 0x1234".
            ulong target = GetTarget(heap);


            /* At the most basic level, to implement !gcroot, simply create a GCRoot object.  Then call
                * EnumerateGCRoots to find unique paths from the root to the target object.  This runs similarly
                * to !gcroot.  Please note you will receive all paths each time this code is run, but the order
                * of roots from run to run are NOT guaranteed.  Note that all operations take a cancellation token
                * which you can use to cancel GCRoot at any time (it will throw an OperationCancelledException).
                */
            GCRoot gcroot = new GCRoot(heap);
            foreach (GCRootPath rootPath in gcroot.EnumerateGCRoots(target, CancellationToken.None))
            {
                Console.Write($"{rootPath.Root} -> ");
                Console.WriteLine(string.Join(" -> ", rootPath.Path.Select(obj => obj.Address)));
            }

            Console.WriteLine();
            // ==========================================

            /* Since GCRoot can take a long time to run, ClrMD provides an event to get progress updates on how far
                * along GCRoot is.  There are some caveats here, in that you only get updates if you have cached the
                * gc heap locally (more on that later).  This is because without pre-fetching and compiling data ahead
                * of time, we have no real way to know how many objects are on the heap.  The intent of this delegate is
                * to allow you to report to the user a rough percentage of how far along things are (such as a progress
                * bar or console output).
                */
            gcroot.ProgressUpdated += delegate (GCRoot source, long current, long total)
            {
                // Note that sometimes we don't know how many total items will be processed in the current
                // phase.  In that case, total will be -1.  (For example, we don't know the total number
                // of handles on the handle table until we enumerate them all.)

                if (total > 0)
                {
                    Console.WriteLine($"heap searched={(int)(100 * current / (float)total)}%");
                }
                else
                {
                    Console.WriteLine($"objects inspected={current}");
                }
            };

            // ==========================================

            /* The next important concept is that GCRoot allows you to trade memory for a faster !gcroot.
                * Due to how memory reads and the clr debugging layer are written, it can take 30-60 minutes
                * to run the full GCRoot algorithm on large dumps using the code above!  To fix that, ClrMD
                * allows you to copy all relevant data into local memory, at which point all GCRoot runs should
                * complete very fast.  Even the worst crash dumps should take <60 seconds to complete.  The
                * downside is this will use a lot of memory (in some cases, multiple gigs of memory), so you have
                * to wrap the cach function in a try/catch for OOM exceptions.
                */

            try
            {
                gcroot.BuildCache(CancellationToken.None);
                // Now GCRoot will run MUCH, MUCH faster for the following code.
            }
            catch (OutOfMemoryException)
            {
                // Whoops, the crash dump in question was too big to read all data into memory.  We will continue
                // on without cached GC data.
            }


            // ==========================================

            /* The next thing to know about GCRoot is there are two ways we can walk the stack of threads looking for
                * roots:  Exact and Fast.  You change this setting with ClrHeap.StackwalkPolicy.  Using a stackwalk policy
                * of exact will show you exactly what the GC sees, eliminating false roots, but it can be slow.  Using the
                * Fast stackwalk policy is very fast, but can over-report roots that are actually just old GC references
                * left on the stack.
                * Unfortunately due to the way that CLR's debugging layer is implemented, using Exact can take a *long*
                * time (10s of minutes if the dump has 1000s of threads).  The default is to let ClrMD decide which policy
                * to use.  Lastly, note that you can choose to omit stack roots entirely by setting StackwalkPolicy to
                * "SkipStack".
                */

            // Let's set a concrete example:
            heap.StackwalkPolicy = ClrRootStackwalkPolicy.Exact;


            // ==========================================

            /* Finally, GCRoot can use parallel processing to use more of the CPU and work in parallel.  This is done
                * automatically (and is on by default)...but only if you use .BuildCache and build the *complete* GC heap
                * in local memory.  Since limitations in ClrMD and the APIs it's built on do not allow multithreading,
                * we only do multithreaded processing if we never need to call into ClrMD while walking objects.
                *
                * One downside is that multithreaded processing means the order of roots enumerated is not stable from
                * run to run.  You can turn off multithreaded processing so that the results of GCRoot are stable between
                * runs (though slower to run), but GCRoot makes NO guarantees about the order of roots you receive, and
                * they may change from build to build of ClrMD or change depending on the other flags set on the GCRoot
                * object.
                */

            // This can be used to turn off multithreaded processing, this property is true by default:
            // gcroot.AllowParallelSearch = false;

            // You can set the maximum number of tasks using this.  The default is Environment.ProcessorCount * 2:
            gcroot.MaximumTasksAllowed = 20;

            // ==========================================

            /* Finally, we will run through the enumeration one more time, but this time using parallel processing
                * and printing out a bunch of status messages for demonstration.
                */

            Console.WriteLine("Get ready for a lot of output, since we are raw printing status updates!");
            Console.WriteLine();
            foreach (GCRootPath rootPath in gcroot.EnumerateGCRoots(target, CancellationToken.None))
            {
                Console.Write($"{rootPath.Root} -> ");
                Console.WriteLine(string.Join(" -> ", rootPath.Path.Select(obj => obj.Address)));
            }
        }

        private static ulong GetTarget(ClrHeap heap)
        {
            // This just returns the one instance of 'TargetType' on the heap.
            return (from obj in heap.EnumerateObjectAddresses()
                    let type = heap.GetObjectType(obj)
                    where type?.Name == "TargetType"
                    select obj).Single();
        }
    }
}
