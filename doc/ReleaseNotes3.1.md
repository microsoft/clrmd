# ClrMD 3.1 Release Notes

Major changes:

1. Marked ClrObject's constructor as `[Obsolete]`.  This will be marked internal soon, instead use `ClrHeap.GetObject(ulong address)`.  This is to support an upcoming change which will make ClrObject's internals work correctly even when we don't have a real ClrType associated with it.
2. Added `ClrType.ThreadStaticFields`.
3. Reworked some of `ClrThreadPool`'s properties.
4. Fixed issues with Hot/Cold size information.
5. Marked `DacLibrary` as internal.  I've refactored how we handle the dac internally, and we no longer want to publish `DacLibrary` as a result.
6. Fixed an issue where `ClrStaticField.ReadStruct` did not work properly.
