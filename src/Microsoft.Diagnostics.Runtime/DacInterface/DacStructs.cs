using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Runtime.InteropServices;

#pragma warning disable 0649
#pragma warning disable 0169
namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RejitData
    {
        private readonly ulong RejitID;
        private readonly uint Flags;
        private readonly ulong NativeCodeAddr;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct DomainLocalModuleData : IDomainLocalModuleData
    {
        public readonly ulong AppDomainAddress;
        public readonly ulong ModuleID;

        public readonly ulong ClassData;
        public readonly ulong DynamicClassTable;
        public readonly ulong GCStaticDataStart;
        public readonly ulong NonGCStaticDataStart;

        ulong IDomainLocalModuleData.AppDomainAddr => AppDomainAddress;
        ulong IDomainLocalModuleData.ModuleID => ModuleID;
        ulong IDomainLocalModuleData.ClassData => ClassData;
        ulong IDomainLocalModuleData.DynamicClassTable => DynamicClassTable;
        ulong IDomainLocalModuleData.GCStaticDataStart => GCStaticDataStart;
        ulong IDomainLocalModuleData.NonGCStaticDataStart => NonGCStaticDataStart;
    }


    [StructLayout(LayoutKind.Sequential)]
    public readonly struct HandleData
    {
        public readonly ulong AppDomain;
        public readonly ulong Handle;
        public readonly ulong Secondary;
        public readonly uint Type;
        public readonly uint StrongReference;

        // For RefCounted Handles
        public readonly uint RefCount;
        public readonly uint JupiterRefCount;
        public readonly uint IsPegged;
    }


    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MethodDescData
    {
        public readonly uint HasNativeCode;
        public readonly uint IsDynamic;
        public readonly short SlotNumber;
        public readonly ulong NativeCodeAddr;

        // Useful for breaking when a method is jitted.
        public readonly ulong AddressOfNativeCodeSlot;

        public readonly ulong MethodDescPtr;
        public readonly ulong MethodTablePtr;
        public readonly ulong ModulePtr;

        public readonly uint MDToken;
        public readonly ulong GCInfo;
        public readonly ulong GCStressCodeCopy;

        // This is only valid if bIsDynamic is true
        public readonly ulong ManagedDynamicMethodObject;

        public readonly ulong RequestedIP;

        // Gives info for the single currently active version of a method
        public readonly RejitData RejitDataCurrent;

        // Gives info corresponding to requestedIP (for !ip2md)
        public readonly RejitData RejitDataRequested;

        // Total number of rejit versions that have been jitted
        public readonly uint JittedRejitVersions;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CodeHeaderData
    {
        public readonly ulong GCInfo;
        public readonly uint JITType;
        public readonly ulong MethodDescPtr;
        public readonly ulong MethodStart;
        public readonly uint MethodSize;
        public readonly ulong ColdRegionStart;
        public readonly uint ColdRegionSize;
        public readonly uint HotRegionSize;
    }


    [StructLayout(LayoutKind.Sequential)]
    public readonly struct StackRefData
    {
        public readonly uint HasRegisterInformation;
        public readonly int Register;
        public readonly int Offset;
        public readonly ulong Address;
        public readonly ulong Object;
        public readonly uint Flags;

        public readonly uint SourceType;
        public readonly ulong Source;
        public readonly ulong StackPointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ThreadLocalModuleData
    {
        public readonly ulong ThreadAddress;
        public readonly ulong ModuleIndex;

        public readonly ulong ClassData;
        public readonly ulong DynamicClassTable;
        public readonly ulong GCStaticDataStart;
        public readonly ulong NonGCStaticDataStart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ThreadPoolData : IThreadPoolData
    {
        public readonly int CpuUtilization;
        public readonly int NumIdleWorkerThreads;
        public readonly int NumWorkingWorkerThreads;
        public readonly int NumRetiredWorkerThreads;
        public readonly int MinLimitTotalWorkerThreads;
        public readonly int MaxLimitTotalWorkerThreads;

        public readonly ulong FirstUnmanagedWorkRequest;

        public readonly ulong HillClimbingLog;
        public readonly int HillClimbingLogFirstIndex;
        public readonly int HillClimbingLogSize;

        public readonly int NumTimers;

        public readonly int NumCPThreads;
        public readonly int NumFreeCPThreads;
        public readonly int MaxFreeCPThreads;
        public readonly int NumRetiredCPThreads;
        public readonly int MaxLimitTotalCPThreads;
        public readonly int CurrentLimitTotalCPThreads;
        public readonly int MinLimitTotalCPThreads;

        public readonly ulong _asyncTimerCallbackCompletionFPtr;

        int IThreadPoolData.MinCP => MinLimitTotalCPThreads;
        int IThreadPoolData.MaxCP => MaxLimitTotalCPThreads;
        int IThreadPoolData.CPU => CpuUtilization;
        int IThreadPoolData.NumFreeCP => NumFreeCPThreads;
        int IThreadPoolData.MaxFreeCP => MaxFreeCPThreads;
        int IThreadPoolData.TotalThreads => NumIdleWorkerThreads + NumWorkingWorkerThreads + NumRetiredWorkerThreads;
        int IThreadPoolData.RunningThreads => NumWorkingWorkerThreads;
        int IThreadPoolData.IdleThreads => NumIdleWorkerThreads;
        int IThreadPoolData.MinThreads => MinLimitTotalWorkerThreads;
        int IThreadPoolData.MaxThreads => MaxLimitTotalWorkerThreads;
        ulong IThreadPoolData.FirstWorkRequest => FirstUnmanagedWorkRequest;
        ulong IThreadPoolData.QueueUserWorkItemCallbackFPtr => 0;
        ulong IThreadPoolData.AsyncCallbackCompletionFPtr => 0;
        ulong IThreadPoolData.AsyncTimerCallbackCompletionFPtr => _asyncTimerCallbackCompletionFPtr;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ModuleData : IModuleData
    {
        public readonly ulong Address;
        public readonly ulong PEFile;
        public readonly ulong ILBase;
        public readonly ulong MetadataStart;
        public readonly ulong MetadataSize;
        public readonly ulong Assembly;
        public readonly uint IsReflection;
        public readonly uint IsPEFile;
        public readonly ulong BaseClassIndex;
        public readonly ulong ModuleID;
        public readonly uint TransientFlags;
        public readonly ulong TypeDefToMethodTableMap;
        public readonly ulong TypeRefToMethodTableMap;
        public readonly ulong MethodDefToDescMap;
        public readonly ulong FieldDefToDescMap;
        public readonly ulong MemberRefToDescMap;
        public readonly ulong FileReferencesMap;
        public readonly ulong ManifestModuleReferencesMap;
        public readonly ulong LookupTableHeap;
        public readonly ulong ThunkHeap;
        public readonly ulong ModuleIndex;

        ulong IModuleData.Assembly => Assembly;
        ulong IModuleData.PEFile => (IsPEFile == 0) ? ILBase : PEFile;
        ulong IModuleData.LookupTableHeap => LookupTableHeap;
        ulong IModuleData.ThunkHeap => ThunkHeap;
        IntPtr IModuleData.LegacyMetaDataImport => IntPtr.Zero;
        ulong IModuleData.ModuleId => ModuleID;
        ulong IModuleData.ModuleIndex => ModuleIndex;
        bool IModuleData.IsReflection => IsReflection != 0;
        bool IModuleData.IsPEFile => IsPEFile != 0;
        ulong IModuleData.ImageBase => ILBase;
        ulong IModuleData.MetdataStart => MetadataStart;
        ulong IModuleData.MetadataLength => MetadataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct V45ObjectData : IObjectData
    {
        public readonly ulong MethodTable;
        public readonly uint ObjectType;
        public readonly ulong Size;
        public readonly ulong ElementTypeHandle;
        public readonly uint ElementType;
        public readonly uint Rank;
        public readonly ulong NumComponents;
        public readonly ulong ComponentSize;
        public readonly ulong ArrayDataPointer;
        public readonly ulong ArrayBoundsPointer;
        public readonly ulong ArrayLowerBoundsPointer;
        public readonly ulong RCW;
        public readonly ulong CCW;

        ClrElementType IObjectData.ElementType => (ClrElementType)ElementType;
        ulong IObjectData.ElementTypeHandle => ElementTypeHandle;
        ulong IObjectData.RCW => RCW;
        ulong IObjectData.CCW => CCW;
        ulong IObjectData.DataPointer => ArrayDataPointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MethodTableData : IMethodTableData
    {
        public readonly uint IsFree; // everything else is NULL if this is true.
        public readonly ulong Module;
        public readonly ulong EEClass;
        public readonly ulong ParentMethodTable;
        public readonly ushort NumInterfaces;
        public readonly ushort NumMethods;
        public readonly ushort NumVtableSlots;
        public readonly ushort NumVirtuals;
        public readonly uint BaseSize;
        public readonly uint ComponentSize;
        public readonly uint Token;
        public readonly uint AttrClass;
        public readonly uint Shared; // flags & enum_flag_DomainNeutral
        public readonly uint Dynamic;
        public readonly uint ContainsPointers;

        uint IMethodTableData.Token => Token;
        ulong IMethodTableData.Module => Module;
        bool IMethodTableData.ContainsPointers => ContainsPointers != 0;
        uint IMethodTableData.BaseSize => BaseSize;
        uint IMethodTableData.ComponentSize => ComponentSize;
        ulong IMethodTableData.EEClass => EEClass;
        bool IMethodTableData.Free => IsFree != 0;
        ulong IMethodTableData.Parent => ParentMethodTable;
        bool IMethodTableData.Shared => Shared != 0;
        uint IMethodTableData.NumMethods => NumMethods;
        ulong IMethodTableData.ElementTypeHandle => throw new NotImplementedException();
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct COMInterfacePointerData
    {
        public readonly ulong MethodTable;
        public readonly ulong InterfacePtr;
        public readonly ulong ComContext;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CCWData : ICCWData
    {
        public readonly ulong OuterIUnknown;
        public readonly ulong ManagedObject;
        public readonly ulong Handle;
        public readonly ulong CCWAddress;

        public readonly int RefCount;
        public readonly int InterfaceCount;
        public readonly uint IsNeutered;

        public readonly int JupiterRefCount;
        public readonly uint IsPegged;
        public readonly uint IsGlobalPegged;
        public readonly uint HasStrongRef;
        public readonly uint IsExtendsCOMObject;
        public readonly uint HasWeakReference;
        public readonly uint IsAggregated;

        ulong ICCWData.IUnknown => OuterIUnknown;
        ulong ICCWData.Object => ManagedObject;
        ulong ICCWData.Handle => Handle;
        ulong ICCWData.CCWAddress => CCWAddress;
        int ICCWData.RefCount => RefCount;
        int ICCWData.JupiterRefCount => JupiterRefCount;
        int ICCWData.InterfaceCount => InterfaceCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RCWData : IRCWData
    {
        public readonly ulong IdentityPointer;
        public readonly ulong IUnknownPointer;
        public readonly ulong ManagedObject;
        public readonly ulong JupiterObject;
        public readonly ulong VTablePointer;
        public readonly ulong CreatorThread;
        public readonly ulong CTXCookie;

        public readonly int RefCount;
        public readonly int InterfaceCount;

        public readonly uint IsJupiterObject;
        public readonly uint SupportsIInspectable;
        public readonly uint IsAggregated;
        public readonly uint IsContained;
        public readonly uint IsFreeThreaded;
        public readonly uint IsDisconnected;

        ulong IRCWData.IdentityPointer => IdentityPointer;
        ulong IRCWData.UnknownPointer => IUnknownPointer;
        ulong IRCWData.ManagedObject => ManagedObject;
        ulong IRCWData.JupiterObject => JupiterObject;
        ulong IRCWData.VTablePtr => VTablePointer;
        ulong IRCWData.CreatorThread => CreatorThread;
        int IRCWData.RefCount => RefCount;
        int IRCWData.InterfaceCount => InterfaceCount;
        bool IRCWData.IsJupiterObject => IsJupiterObject != 0;
        bool IRCWData.IsDisconnected => IsDisconnected != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct WorkRequestData
    {
        public readonly ulong Function;
        public readonly ulong Context;
        public readonly ulong NextWorkRequest;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ThreadStoreData : IThreadStoreData
    {
        public readonly int ThreadCount;
        public readonly int UnstartedThreadCount;
        public readonly int BackgroundThreadCount;
        public readonly int PendingThreadCount;
        public readonly int DeadThreadCount;
        public readonly ulong FirstThread;
        public readonly ulong FinalizerThread;
        public readonly ulong GCThread;
        public readonly uint HostConfig;

        ulong IThreadStoreData.Finalizer => FinalizerThread;
        int IThreadStoreData.Count => ThreadCount;
        ulong IThreadStoreData.FirstThread => FirstThread;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct SyncBlockData : ISyncBlkData
    {
        public readonly ulong Object;
        public readonly uint Free;
        public readonly ulong Address;
        public readonly uint COMFlags;
        public readonly uint MonitorHeld;
        public readonly uint Recursion;
        public readonly ulong HoldingThread;
        public readonly uint AdditionalThreadCount;
        public readonly ulong AppDomain;
        public readonly uint TotalSyncBlockCount;

        bool ISyncBlkData.Free => Free != 0;
        ulong ISyncBlkData.Object => Object;
        bool ISyncBlkData.MonitorHeld => MonitorHeld != 0;
        uint ISyncBlkData.Recursion => Recursion;
        uint ISyncBlkData.TotalCount => TotalSyncBlockCount;
        ulong ISyncBlkData.OwningThread => HoldingThread;
        ulong ISyncBlkData.Address => Address;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct V4FieldInfo : IFieldInfo
    {
        public readonly short NumInstanceFields;
        public readonly short NumStaticFields;
        public readonly short NumThreadStaticFields;
        public readonly ulong FirstFieldAddress; // If non-null, you can retrieve more
        public readonly short ContextStaticOffset;
        public readonly short ContextStaticsSize;

        uint IFieldInfo.InstanceFields => (uint)NumInstanceFields;
        uint IFieldInfo.StaticFields => (uint)NumStaticFields;
        uint IFieldInfo.ThreadStaticFields => (uint)NumThreadStaticFields;
        ulong IFieldInfo.FirstField => FirstFieldAddress;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FieldData : IFieldData
    {
        public readonly uint ElementType;      // CorElementType
        public readonly uint SigType;   // CorElementType
        public readonly ulong TypeMethodTable; // NULL if Type is not loaded
        public readonly ulong TypeModule;
        public readonly uint MDType;
        public readonly uint MDField;
        public readonly ulong MTOfEnclosingClass;
        public readonly uint Offset;
        public readonly uint IsThreadLocal;
        public readonly uint IsContextLocal;
        public readonly uint IsStatic;
        public readonly ulong NextField;

        uint IFieldData.CorElementType => ElementType;
        uint IFieldData.SigType => SigType;
        ulong IFieldData.TypeMethodTable => TypeMethodTable;
        ulong IFieldData.Module => TypeModule;
        uint IFieldData.TypeToken => MDType;
        uint IFieldData.FieldToken => MDField;
        ulong IFieldData.EnclosingMethodTable => MTOfEnclosingClass;
        uint IFieldData.Offset => Offset;
        bool IFieldData.IsThreadLocal => IsThreadLocal != 0;
        bool IFieldData.IsContextLocal => IsContextLocal != 0;
        bool IFieldData.IsStatic => IsStatic != 0;
        ulong IFieldData.NextField => NextField;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CommonMethodTables
    {
        public readonly ulong ArrayMethodTable;
        public readonly ulong StringMethodTable;
        public readonly ulong ObjectMethodTable;
        public readonly ulong ExceptionMethodTable;
        public readonly ulong FreeMethodTable;

        internal bool Validate()
        {
            return ArrayMethodTable != 0 &&
                StringMethodTable != 0 &&
                ObjectMethodTable != 0 &&
                ExceptionMethodTable != 0 &&
                FreeMethodTable != 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct AssemblyData : IAssemblyData
    {
        public readonly ulong Address;
        public readonly ulong ClassLoader;
        public readonly ulong ParentDomain;
        public readonly ulong AppDomain;
        public readonly ulong AssemblySecurityDescriptor;
        public readonly int Dynamic;
        public readonly int ModuleCount;
        public readonly uint LoadContext;
        public readonly int IsDomainNeutral;
        public readonly uint LocationFlags;

        ulong IAssemblyData.Address => Address;
        ulong IAssemblyData.ParentDomain => ParentDomain;
        ulong IAssemblyData.AppDomain => AppDomain;
        bool IAssemblyData.IsDynamic => Dynamic != 0;
        bool IAssemblyData.IsDomainNeutral => IsDomainNeutral != 0;
        int IAssemblyData.ModuleCount => ModuleCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct AppDomainData : IAppDomainData
    {
        public readonly ulong Address;
        public readonly ulong SecurityDescriptor;
        public readonly ulong LowFrequencyHeap;
        public readonly ulong HighFrequencyHeap;
        public readonly ulong StubHeap;
        public readonly ulong DomainLocalBlock;
        public readonly ulong DomainLocalModules;
        public readonly int Id;
        public readonly int AssemblyCount;
        public readonly int FailedAssemblyCount;
        public readonly int Stage;

        int IAppDomainData.Id => Id;
        ulong IAppDomainData.Address => Address;
        ulong IAppDomainData.LowFrequencyHeap => LowFrequencyHeap;
        ulong IAppDomainData.HighFrequencyHeap => HighFrequencyHeap;
        ulong IAppDomainData.StubHeap => StubHeap;
        int IAppDomainData.AssemblyCount => AssemblyCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct AppDomainStoreData : IAppDomainStoreData
    {
        public readonly ulong SharedDomain;
        public readonly ulong SystemDomain;
        public readonly int AppDomainCount;

        ulong IAppDomainStoreData.SharedDomain => SharedDomain;
        ulong IAppDomainStoreData.SystemDomain => SystemDomain;
        int IAppDomainStoreData.Count => AppDomainCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ThreadData : IThreadData
    {
        public readonly uint ManagedThreadId;
        public readonly uint OSThreadId;
        public readonly int State;
        public readonly uint PreemptiveGCDisabled;
        public readonly ulong AllocationContextPointer;
        public readonly ulong AllocationContextLimit;
        public readonly ulong Context;
        public readonly ulong Domain;
        public readonly ulong Frame;
        public readonly uint LockCount;
        public readonly ulong FirstNestedException;
        public readonly ulong Teb;
        public readonly ulong FiberData;
        public readonly ulong LastThrownObjectHandle;
        public readonly ulong NextThread;

        ulong IThreadData.Next => IntPtr.Size == 8 ? NextThread : (uint)NextThread;
        ulong IThreadData.AllocPtr => IntPtr.Size == 8 ? AllocationContextPointer : (uint)AllocationContextPointer;
        ulong IThreadData.AllocLimit => IntPtr.Size == 8 ? AllocationContextLimit : (uint)AllocationContextLimit;
        uint IThreadData.OSThreadID => OSThreadId;
        ulong IThreadData.Teb => IntPtr.Size == 8 ? Teb : (uint)Teb;
        ulong IThreadData.AppDomain => Domain;
        uint IThreadData.LockCount => LockCount;
        int IThreadData.State => State;
        ulong IThreadData.ExceptionPtr => LastThrownObjectHandle;
        uint IThreadData.ManagedThreadID => ManagedThreadId;
        bool IThreadData.Preemptive => PreemptiveGCDisabled == 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GCInfo : IGCInfo
    {
        public readonly int ServerMode;
        public readonly int GCStructuresValid;
        public readonly int HeapCount;
        public readonly int MaxGeneration;

        bool IGCInfo.ServerMode => ServerMode != 0;
        int IGCInfo.HeapCount => HeapCount;
        int IGCInfo.MaxGeneration => MaxGeneration;
        bool IGCInfo.GCStructuresValid => GCStructuresValid != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct SegmentData : ISegmentData
    {
        public readonly ulong Address;
        public readonly ulong Allocated;
        public readonly ulong Committed;
        public readonly ulong Reserved;
        public readonly ulong Used;
        public readonly ulong Mem;
        public readonly ulong Next;
        public readonly ulong Heap;
        public readonly ulong HighAllocMark;
        public readonly IntPtr Flags;
        public readonly ulong BackgroundAllocated;

        ulong ISegmentData.Address => Address;
        ulong ISegmentData.Next => Next;
        ulong ISegmentData.Start => Mem;
        ulong ISegmentData.End => Allocated;
        ulong ISegmentData.Reserved => Reserved;
        ulong ISegmentData.Committed => Committed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GenerationData
    {
        public readonly ulong StartSegment;
        public readonly ulong AllocationStart;

        // These are examined only for generation 0, otherwise NULL
        public readonly ulong AllocationContextPointer;
        public readonly ulong AllocationContextLimit;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct HeapDetails : IHeapDetails
    {
        public readonly ulong Address; // Only filled in in server mode, otherwise NULL
        public readonly ulong Allocated;
        public readonly ulong MarkArray;
        public readonly ulong CAllocateLH;
        public readonly ulong NextSweepObj;
        public readonly ulong SavedSweepEphemeralSeg;
        public readonly ulong SavedSweepEphemeralStart;
        public readonly ulong BackgroundSavedLowestAddress;
        public readonly ulong BackgroundSavedHighestAddress;

        public readonly GenerationData GenerationTable0;
        public readonly GenerationData GenerationTable1;
        public readonly GenerationData GenerationTable2;
        public readonly GenerationData GenerationTable3;
        public readonly ulong EphemeralHeapSegment;
        public readonly ulong FinalizationFillPointers0;
        public readonly ulong FinalizationFillPointers1;
        public readonly ulong FinalizationFillPointers2;
        public readonly ulong FinalizationFillPointers3;
        public readonly ulong FinalizationFillPointers4;
        public readonly ulong FinalizationFillPointers5;
        public readonly ulong FinalizationFillPointers6;
        public readonly ulong LowestAddress;
        public readonly ulong HighestAddress;
        public readonly ulong CardTable;

        ulong IHeapDetails.FirstHeapSegment => GenerationTable2.StartSegment;

        ulong IHeapDetails.FirstLargeHeapSegment => GenerationTable3.StartSegment;
        ulong IHeapDetails.EphemeralSegment => EphemeralHeapSegment;
        ulong IHeapDetails.EphemeralEnd => Allocated;
        ulong IHeapDetails.EphemeralAllocContextPtr => GenerationTable0.AllocationContextPointer;
        ulong IHeapDetails.EphemeralAllocContextLimit => GenerationTable0.AllocationContextLimit;
        ulong IHeapDetails.FQAllObjectsStart => FinalizationFillPointers0;
        ulong IHeapDetails.FQAllObjectsStop => FinalizationFillPointers3;
        ulong IHeapDetails.FQRootsStart => FinalizationFillPointers3;
        ulong IHeapDetails.FQRootsStop => FinalizationFillPointers5;
        ulong IHeapDetails.Gen0Start => GenerationTable0.AllocationStart;
        ulong IHeapDetails.Gen0Stop => Allocated;
        ulong IHeapDetails.Gen1Start => GenerationTable1.AllocationStart;
        ulong IHeapDetails.Gen1Stop => GenerationTable0.AllocationStart;
        ulong IHeapDetails.Gen2Start => GenerationTable2.AllocationStart;
        ulong IHeapDetails.Gen2Stop => GenerationTable1.AllocationStart;
    }

    public enum CodeHeapType : int
    {
        Loader,
        Host,
        Unknown
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct JitCodeHeapInfo : ICodeHeap
    {
        public readonly CodeHeapType Type;
        public readonly ulong Address;
        public readonly ulong CurrentAddress;

        CodeHeapType ICodeHeap.Type => Type;
        ulong ICodeHeap.Address => Address;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct JitManagerInfo
    {
        public readonly ulong Address;
        public readonly CodeHeapType Type;
        public readonly ulong HeapList;
    }
}
