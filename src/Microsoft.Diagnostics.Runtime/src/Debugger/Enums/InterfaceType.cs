namespace Microsoft.Diagnostics.Runtime.Interop
{
    public enum INTERFACE_TYPE : int
    {
        InterfaceTypeUndefined = -1,
        Internal,
        Isa,
        Eisa,
        MicroChannel,
        TurboChannel,
        PCIBus,
        VMEBus,
        NuBus,
        PCMCIABus,
        CBus,
        MPIBus,
        MPSABus,
        ProcessorInternal,
        InternalPowerBus,
        PNPISABus,
        PNPBus,
        Vmcs,
        MaximumInterfaceType
    }
}