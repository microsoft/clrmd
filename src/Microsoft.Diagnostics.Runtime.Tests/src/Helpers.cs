// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public static class Helpers
    {
        public static IEnumerable<ClrObject> GetObjectsOfType(this ClrHeap heap, string name)
        {
            return from obj in heap.EnumerateObjects()
                   where obj.Type?.Name == name
                   select obj;
        }

        public static IEnumerable<ClrType> EnumerateTypes(this ClrModule module)
        {
            ClrRuntime runtime = module.AppDomain.Runtime;
            foreach ((ulong mt, int _) in module.EnumerateTypeDefToMethodTableMap())
            {
                ClrType type = runtime.GetTypeByMethodTable(mt);
                if (type != null)
                    yield return type;
            }
        }

        public static IEnumerable<ClrModule> EnumerateModules(this ClrRuntime runtime) => runtime.AppDomains.SelectMany(ad => ad.Modules);

        public static ClrType GetTypeByName(this ClrModule module, string typeName)
        {
            ClrRuntime runtime = module.AppDomain.Runtime;
            foreach ((ulong mt, int _) in module.EnumerateTypeDefToMethodTableMap())
            {
                ClrType type = runtime.GetTypeByMethodTable(mt);
                if (type.Name == typeName)
                    return type;
            }

            return null;
        }

        public static ClrObject GetStaticObjectValue(this ClrType mainType, string fieldName)
        {
            ClrStaticField field = mainType.GetStaticFieldByName(fieldName);
            return field.ReadObject(mainType.Module.AppDomain);
        }

        public static ClrModule GetMainModule(this ClrRuntime runtime)
        {
            // .NET Core SDK 3.x creates an executable host by default (FDE)
            return runtime.AppDomains.SelectMany(ad => ad.Modules).Single(m => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? m.Name.EndsWith(".exe") : File.Exists(Path.ChangeExtension(m.Name, null)));
        }

        public static ClrMethod GetMethod(this ClrType type, string name)
        {
            return GetMethods(type, name).Single();
        }

        public static IEnumerable<ClrMethod> GetMethods(this ClrType type, string name)
        {
            return type.Methods.Where(m => m.Name == name);
        }

        public static HashSet<T> Unique<T>(this IEnumerable<T> self)
        {
            HashSet<T> set = new HashSet<T>();
            foreach (T t in self)
                set.Add(t);

            return set;
        }

        public static ClrAppDomain GetDomainByName(this ClrRuntime runtime, string domainName)
        {
            return runtime.AppDomains.Single(ad => ad.Name == domainName);
        }

        public static ClrModule GetModule(this ClrRuntime runtime, string fileName)
        {
            return (from module in runtime.AppDomains.SelectMany(ad => ad.Modules)
                    let file = Path.GetFileName(module.Name)
                    where file.Equals(fileName, StringComparison.OrdinalIgnoreCase)
                    select module).FirstOrDefault();
        }

        public static ClrThread GetMainThread(this ClrRuntime runtime)
        {
            ClrThread thread = runtime.Threads.Single(t => !t.IsBackground);
            return thread;
        }

        public static ClrStackFrame GetFrame(this ClrThread thread, string functionName)
        {
            return thread.EnumerateStackTrace().Single(sf => sf.Method != null && sf.Method.Name == functionName);
        }

        public static string TestWorkingDirectory
        {
            get => _userSetWorkingPath ?? _workingPath.Value;
            set => _userSetWorkingPath = value;
        }

        private static string _userSetWorkingPath;
        private static readonly Lazy<string> _workingPath = new Lazy<string>(CreateWorkingPath, true);

        private static string CreateWorkingPath()
        {
            Random r = new Random();
            string path;
            do
            {
                path = Path.Combine(Environment.CurrentDirectory, TempRoot + r.Next());
            } while (Directory.Exists(path));

            Directory.CreateDirectory(path);
            return path;
        }

        internal static readonly string TempRoot = "clrmd_removeme_";
    }

    public class GlobalCleanup
    {
        public static void AssemblyCleanup()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            foreach (string directory in Directory.EnumerateDirectories(Environment.CurrentDirectory))
                if (directory.Contains(Helpers.TempRoot))
                    Directory.Delete(directory, true);
        }
    }
}
