namespace Microsoft.Diagnostics.Runtime.Interop
{
    public enum DEBUG_DATA : uint
    {
        KPCR_OFFSET = 0,
        KPRCB_OFFSET = 1,
        KTHREAD_OFFSET = 2,
        BASE_TRANSLATION_VIRTUAL_OFFSET = 3,
        PROCESSOR_IDENTIFICATION = 4,
        PROCESSOR_SPEED = 5,
    }
}