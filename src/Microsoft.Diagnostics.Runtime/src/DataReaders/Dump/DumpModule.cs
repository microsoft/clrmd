using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  /// <summary>
  /// Represents a native module in a dump file. This is a flyweight object.
  /// </summary>
  internal class DumpModule
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="owner">owning DumpReader</param>
    /// <param name="raw">unmanaged dump structure describing the module</param>
    internal DumpModule(DumpReader owner, DumpReader.DumpNative.MINIDUMP_MODULE raw)
    {
      _raw = raw;
      _owner = owner;
    }
    private DumpReader.DumpNative.MINIDUMP_MODULE _raw;
    private DumpReader _owner;

    internal DumpReader.DumpNative.MINIDUMP_MODULE Raw
    {
      get
      {
        return _raw;
      }
    }

    // Since new DumpModule objects are created on each request, override hash code and equals
    // to provide equality so that we can use them in hashes and collections.
    public override bool Equals(object obj)
    {
      DumpModule other = obj as DumpModule;
      if (other == null) return false;
      return (other._owner == _owner) && (other._raw == _raw);
    }

    // Override of GetHashCode
    public override int GetHashCode()
    {
      // TimeStamp and Checksum are already great 32-bit hash values. 
      // CheckSum may be 0, so use TimeStamp            
      return unchecked((int)_raw.TimeDateStamp);
    }

    /// <summary>
    ///  Usually, the full filename of the module. Since the dump may not be captured on the local
    ///  machine, be careful of using this filename with the local file system.
    ///  In some cases, this could be a short filename, or unavailable.
    /// </summary>
    public string FullName
    {
      get
      {
        DumpReader.DumpNative.RVA rva = _raw.ModuleNameRva;
        DumpPointer ptr = _owner.TranslateRVA(rva);

        string name = _owner.GetString(ptr);
        return name;
      }
    }

    /// <summary>
    /// Base address within the target of where this module is loaded.
    /// </summary>
    public ulong BaseAddress
    {
      get
      {
        return _raw.BaseOfImage;
      }
    }

    /// <summary>
    /// Size of this module in bytes as loaded in the target.
    /// </summary>
    public UInt32 Size
    {
      get
      {
        return _raw.SizeOfImage;
      }
    }

    /// <summary>
    /// UTC Time stamp of module. This is based off a 32-bit value and will overflow in 2038.
    /// This is different than any of the filestamps. Call ToLocalTime() to convert from UTC.
    /// </summary>
    public DateTime Timestamp
    {
      get
      {
        return _raw.Timestamp;
      }
    }

    /// <summary>
    /// Gets the raw 32 bit time stamp. Use the Timestamp property to get this as a System.DateTime.
    /// </summary>
    public uint RawTimestamp
    {
      get
      {
        return _raw.TimeDateStamp;
      }
    }
  }
}