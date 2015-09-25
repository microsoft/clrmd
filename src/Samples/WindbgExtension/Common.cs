using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using RGiesecke.DllExport;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace WindbgExtension
{
    public partial class DebuggerExtensions
    {
        public static IDebugClient DebugClient { get; private set; }
        public static DataTarget DataTarget { get; private set; }
        public static ClrRuntime Runtime { get; private set; }

        static DebuggerExtensions()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        static readonly string ClrMD = "Microsoft.Diagnostics.Runtime";
        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            if (args.Name.Contains(ClrMD))
            {
                string codebase = Assembly.GetExecutingAssembly().CodeBase;

                if (codebase.StartsWith("file://"))
                    codebase = codebase.Substring(8).Replace('/', '\\');

                string directory = Path.GetDirectoryName(codebase);
                string path = Path.Combine(directory, ClrMD) + ".dll";
                return Assembly.LoadFile(path);
            }

            return null;
        }

        private static bool InitApi(IntPtr ptrClient)
        {
            // On our first call to the API:
            //   1. Store a copy of IDebugClient in DebugClient.
            //   2. Replace Console's output stream to be the debugger window.
            //   3. Create an instance of DataTarget using the IDebugClient.
            if (DebugClient == null)
            {
                object client = Marshal.GetUniqueObjectForIUnknown(ptrClient);
                DebugClient = (IDebugClient)client;

                var stream = new StreamWriter(new DbgEngStream(DebugClient));
                stream.AutoFlush = true;
                Console.SetOut(stream);

                DataTarget = Microsoft.Diagnostics.Runtime.DataTarget.CreateFromDebuggerInterface(DebugClient);
            }

            // If our ClrRuntime instance is null, it means that this is our first call, or
            // that the dac wasn't loaded on any previous call.  Find the dac loaded in the
            // process (the user must use .cordll), then construct our runtime from it.
            if (Runtime == null)
            {
                // Just find a module named mscordacwks and assume it's the one the user
                // loaded into windbg.
                Process p = Process.GetCurrentProcess();
                foreach (ProcessModule module in p.Modules)
                {
                    if (module.FileName.ToLower().Contains("mscordacwks"))
                    {
                        // TODO:  This does not support side-by-side CLRs.
                        Runtime = DataTarget.ClrVersions.Single().CreateRuntime(module.FileName);
                        break;
                    }
                }

                // Otherwise, the user didn't run .cordll.
                if (Runtime == null)
                {
                    Console.WriteLine("Mscordacwks.dll not loaded into the debugger.");
                    Console.WriteLine("Run .cordll to load the dac before running this command.");
                }
            }
            else
            {
                // If we already had a runtime, flush it for this use.  This is ONLY required
                // for a live process or iDNA trace.  If you use the IDebug* apis to detect
                // that we are debugging a crash dump you may skip this call for better perf.
                Runtime.Flush();
            }

            return Runtime != null;
        }

        [DllExport("DebugExtensionInitialize")]
        public static int DebugExtensionInitialize(ref uint version, ref uint flags)
        {
            // Set the extension version to 1, which expects exports with this signature:
            //      void _stdcall function(IDebugClient *client, const char *args)
            version = DEBUG_EXTENSION_VERSION(1, 0);
            flags = 0;
            return 0;
        }

        static uint DEBUG_EXTENSION_VERSION(uint Major, uint Minor)
        {
            return ((((Major) & 0xffff) << 16) | ((Minor) & 0xffff));
        }
    }
    
    class DbgEngStream : Stream
    {
        public void Clear()
        {
            while (Marshal.ReleaseComObject(m_client) > 0) { }
            while (Marshal.ReleaseComObject(m_control) > 0) { }
        }

        IDebugClient m_client;
        private IDebugControl m_control;
        public DbgEngStream(IDebugClient client)
        {
            m_client = client;
            m_control = (IDebugControl)client;
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get { return -1; }
        }

        public override long Position
        {
            get
            {
                return 0;
            }
            set
            {
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            UTF8Encoding enc = new UTF8Encoding();
            string str = enc.GetString(buffer, offset, count);
            m_control.ControlledOutput(DEBUG_OUTCTL.ALL_CLIENTS, DEBUG_OUTPUT.NORMAL, str);
        }
    }
}
