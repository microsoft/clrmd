# The GC Heap

## Introduction to the GC Heap

CLR's GC is a generational, mark and sweep GC. There are 3 generations in the
process, gen0, gen1, and gen2. Objects are allocated in gen0, and whenever an
object survives a GC they are promoted to the next generation (thus a gen0
object becomes a gen2 object and a gen1 object becomes a gen2 object). The only
exception to this rule are large objects. Large objects are allocated directly
into gen2 (on a special segment) and are never relocated. An object is
considered a "large object" if it takes up more than 85,000 bytes.

There are also two types of GC's: the workstation GC and the server GC. There
are two primary differences between a workstation GC and a server GC from a
diagnostics standpoint: First, the workstation GC runs on only one thread
whereas the server GC runs on multiple threads. Second (and related to the first
point), a workstation GC has only one logical heap whereas the server GC has as
many logical heaps as there are cores on the machine.

When you make an allocation with the server GC running, the newly allocated
object goes into the logical heap associated with the core that thread happened
to be running on. In general, the distinction between the two types of GCs does
not matter for diagnostics. The only real case where you need to care about the
logical heaps in the process is when they become unbalanced...when one heap has
had much more allocated on it than other heaps. This can lead to a performance
issue where GCs pause the process for longer than it should. (An example of
displaying heap balance is shown later.)

Each logical heap in the process has a number of heap Segments associated with
them. A segment is a region of memory which contains managed objects. There are
three kinds of GC segments:

Ephemeral segments, gen2 segments, and large object segments. There is exactly 1
ephemeral segment per logical heap, and the ephemeral segment contains gen0,
gen1, and gen2 objects. When we run out of space on the ephemeral segment, we
allocate a gen2 segment and move some (or all) gen2 objects from the ephermal
segment to the new segment. If there's already a gen2 segment for that logical
heap, we will continue to move gen2 objects out of the ephemeral segment onto
the gen2 segment until we run out of room. There can be any number of gen2
segments per logical heap.

Finally, there are large object segments. When a large object (85,000 bytes or
more) are allocated, we allocate this object directly into gen2. We do not,
however, use the gen2 segments to do this. Instead we allocate these objects in
"large object segments" directly. Large object segments have two main
properties: all objects in them are considered gen2 and no object in them may be
relocated (we treat objects in these segments as pinned).

## Getting the Heap and Walking Segments

The `CLRRuntime` object has a function called GetHeap, which returns a `GCHeap`
object. The `GCHeap` object, among other things, allows you to walk each segment
in the process. Here's a simple example of walking each segment and printing out
data for each segment:

    CLRRuntime runtime = ...;
    Console.WriteLine("{0,12} {1,12} {2,12} {3,12} {4,4} {5}", "Start", "End", "Committed", "Reserved", "Heap", "Type");
    foreach (GCHeapSegment segment in heap.Segments)
    {
        string type;
        if (segment.Ephemeral)
            type = "Ephemeral";
        else if (segment.Large)
            type = "Large";
        else
            type = "Gen2";

        Console.WriteLine("{0,12:X} {1,12:X} {2,12:X} {3,12:X} {4,4} {5}", segment.Start, segment.End, segment.Committed, segment.Reserved, segment.ProcessorAffinity, type);
    }

As you can see, each `GCSegment` object gives you a Start address for the
beginning of the GC segment and the End address (which is the address after the
end of the last object). You also have access to the Committed line, that is,
the address which is the limit of what we have committed (so Committed - End is
the amount of memory we have committed but not filled). Similarly we also give
you the reserved line: the limit of the memory we have reserved for the segment.

Note that the `GCSegment.ProcessorAffinity` is actually the logical GC Heap to
which the segment belongs. Here is a simple linq query which will print out a
table showing logical heap balance:

    foreach (var item in (from seg in heap.Segments
                          group seg by seg.ProcessorAffinity into g
                          orderby g.Key
                          select new
                          {
                              Heap = g.Key,
                              Size = g.Sum(p=>(uint)p.Length)
                          }))
    {
        Console.WriteLine("Heap {0,2}: {1:n0} bytes", item.Heap, item.Size);
    }

As mentioned before, logical heap imbalance in server GC can cause perf issues.

## Walking Managed Objects in the Process

As mentioned before, GC segments contain managed objects. You can walk all
objects on a segment by starting at `GCHeapSegment.FirstObject` and repeatedly
calling `GCHeapSegment.NextObject` until it returns 0. To get the type of an
object, you can call GCHeap.GetObjectType. This returns a `GCHeapType` object,
which we will cover in more detail in a later tutorial.

Here is an example of walking each object on each segment in the process and
printing the address, size, generation, and type of the object:

    CLRRuntime runtime = ...;
    GCHeap heap = runtime.GetHeap();

    if (!heap.CanWalkHeap)
    {
        Console.WriteLine("Cannot walk the heap!");
    }
    else
    {
      foreach (GCHeapSegment seg in heap.Segments)
      {
          for (ulong obj = seg.FirstObject; obj != 0; obj = seg.NextObject(obj))
          {
              GCHeapType type = heap.GetObjectType(obj);

              // If heap corruption, continue past this object.
              if (type == null)
                  continue;

              ulong size = type.GetSize(obj);
              Console.WriteLine("{0,12:X} {1,8:n0} {2,1:n0} {3}", obj, size, seg.GetGeneration(obj), type.Name);
          }
      }
  }

There are two parts in this example you should pay attention to. First is
checking the `GCHeap.CanWalkHeap` property. This property specifies whether the
process is in a state where you can reliably walk the heap. If the crashdump
was taken during the middle of a GC, the GC could have been relocating objects.
At which point a linear walk of the GC heap is not possible. If this is the
case, `CanWalkHeap` will return `false`.

Second, you need to check the return value of GetObjectType to make sure it's
non-null. `GCHeapSegment.NextObject` does not attempt to detect heap corruption,
so it is possible `GetObjectType` will return null if the address that
NextObject returns is a corrupt object.

## Walking objects without walking the segments

There is another way to walk the heap, one which takes far less code than
walking each segment: GCHeap.EnumerateObjects. Here is an example:

    CLRRuntime runtime = ...;
    GCHeap heap = runtime.GetHeap();

    if (!heap.CanWalkHeap)
    {
        Console.WriteLine("Cannot walk the heap!");
    }
    else
    {
      foreach (ulong obj in heap.EnumerateObjects())
      {
          GCHeapType type = heap.GetObjectType(obj);

          // If heap corruption, continue past this object.
          if (type == null)
              continue;

          ulong size = type.GetSize(obj);
          Console.WriteLine("{0,12:X} {1,8:n0} {2,1:n0} {3}", obj, size, heap.GetObjectGeneration(obj), type.Name);
      }
  }

The above code's results are equivalent to the one above it. In general, you
should choose the heap walking approach that best fits your scenario. In
general, if you need to walk only portions of the heap (such as only gen0
objects) or if you need segment data (such as what generation an object is), you
should use the first code. If you simply need to walk all objects on the heap,
use the second code.

## A non-linear heap walk

The approach above is a good way to to walk every object on the heap. But what
if you want to only walk a subset of objects? For example, let's say you have an
object and you want to know all of the objects it points to, and all the objects
those point to, and so on. This is what we call the `!objsize` algorithm.

(If you are not familiar with `!objsize` in SOS, this command takes an object as
(a parameter and counts the number of objects it keeps alive as well as reports
(the total size of all objects the given object keeps alive.)

Given an object, you can enumerate all objects it points to using
`GCHeapType.Enumerate` object references. We will use that function to implement
`objsize`:

    private static void ObjSize(GCHeap heap, ulong obj, out uint count, out ulong size)
    {
        // Evaluation stack
        Stack<ulong> eval = new Stack<ulong>();

        // To make sure we don't count the same object twice, we'll keep a set of all objects
        // we've seen before.  Note the ObjectSet here is basically just "HashSet<ulong>".
        // However, HashSet<ulong> is *extremely* memory inefficient.  So we use our own to
        // avoid OOMs.
        ObjectSet considered = new ObjectSet(heap);

        count = 0;
        size = 0;
        eval.Push(obj);

        while (eval.Count > 0)
        {
            // Pop an object, ignore it if we've seen it before.
            obj = eval.Pop();
            if (considered.Contains(obj))
                continue;

            considered.Add(obj);

            // Grab the type. We will only get null here in the case of heap corruption.
            GCHeapType type = heap.GetObjectType(obj);
            if (type == null)
                continue;

            count++;
            size += type.GetSize(obj);

            // Now enumerate all objects that this object points to, add them to the
            // evaluation stack if we haven't seen them before.
            type.EnumerateRefsOfObject(obj, delegate(ulong child, int offset)
            {
                if (child != 0 && !considered.Contains(child))
                    eval.Push(child);
            });
        }
    }

### Why do we need EnumerateRefsOfObject?

You might be wondering why we need `EnumerateRefsOfObject` at all. As you will
see in the next tutorial, you can walk each field in the object and get its
value. You could implement this algorithm by walking fields instead. However,
`EnumerateRefsOfObject` is much, much faster. It uses the same algorithm the GC
does to get object references out of the object, which is far more efficient
than walking fields to look for objects.

## Conclusion

As you can see, it doesn't take much work to walk the heap. To do anything
useful with the objects you get, though, you will need to work with the
`GCHeapType` for that object. In the next tutorial we will fully explore types
in CLRMD.

Next Tutorial: [Types and Fields in CLRMD](TypesAndFields.md)