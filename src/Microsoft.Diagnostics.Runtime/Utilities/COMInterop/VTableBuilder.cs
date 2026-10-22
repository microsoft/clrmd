// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Builds an individual VTable for a COM object.
    /// </summary>
    public sealed unsafe class VTableBuilder
    {
        private readonly Guid _guid;
        private readonly COMCallableIUnknown _wrapper;
        private readonly bool _forceValidation;
        private readonly List<(Delegate Delegate, IntPtr Method)> _methods = new();

        private bool _complete;

        internal VTableBuilder(COMCallableIUnknown wrapper, Guid guid, bool forceValidation)
        {
            _guid = guid;
            _wrapper = wrapper;
            _forceValidation = forceValidation;
        }

        /// <summary>
        /// Adds a method with an erased delegate type to be the next function in the VTable.
        /// </summary>
        /// <param name="func">The function to add to the next slot of the VTable.</param>
        [RequiresDynamicCode("The delegate type must be known at compile time. Use AddMethod<TDelegate> for NativeAOT.")]
        [RequiresUnreferencedCode("Signature validation requires delegate Invoke metadata. Use AddMethod<TDelegate> to register without reflective validation.")]
        // Keep Delegate-typed arguments on the legacy path instead of inferring TDelegate as Delegate.
        public void AddMethod(Delegate func) => AddMethod(func, validate: false);

        /// <summary>
        /// Adds a method to be the next function in the VTable.
        /// </summary>
        /// <param name="validate">Whether to validate the delegate matches requirements.</param>
        /// <param name="func">The function to add to the next slot of the VTable.</param>
        [RequiresDynamicCode("The delegate type must be known at compile time. Use AddMethod<TDelegate> for NativeAOT.")]
        [RequiresUnreferencedCode("Signature validation requires delegate Invoke metadata. Use AddMethod<TDelegate> to register without reflective validation.")]
        public void AddMethod(Delegate func, bool validate = false)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));

            if (_complete)
                throw new InvalidOperationException();

            if (_forceValidation || validate)
            {
                ParameterInfo[]? parameters = func.GetType().GetMethod("Invoke")?.GetParameters();
                if (parameters is null || parameters.Length == 0 || parameters[0].ParameterType != typeof(IntPtr))
                    throw new InvalidOperationException("The delegate's first parameter must be IntPtr.");
            }

            IntPtr method = Marshal.GetFunctionPointerForDelegate(func);
            _methods.Add((func, method));
        }

        /// <summary>
        /// Adds a method with a statically known delegate type to be the next function in the VTable.
        /// </summary>
        /// <typeparam name="TDelegate">The concrete delegate type, which must exactly match the runtime type of <paramref name="callback"/>.</typeparam>
        /// <param name="callback">The callback to add to the next slot of the VTable.</param>
        /// <exception cref="ArgumentException">The runtime type of <paramref name="callback"/> does not match <typeparamref name="TDelegate"/>.</exception>
        /// <remarks>
        /// This overload does not perform signature validation, even when validation was requested through
        /// <see cref="COMCallableIUnknown.AddInterface"/>. The caller must ensure that the first parameter is
        /// <see cref="IntPtr"/> for the COM this pointer and that the signature, calling convention, and marshalling match the native method.
        /// NativeAOT still requires marshalling support for the declared delegate signature.
        /// </remarks>
        public void AddMethod<TDelegate>(TDelegate callback) where TDelegate : Delegate
        {
            if (callback is null)
                throw new ArgumentNullException(nameof(callback));

            if (_complete)
                throw new InvalidOperationException();

            if (callback.GetType() != typeof(TDelegate))
                throw new ArgumentException("The callback's runtime type must exactly match TDelegate.", nameof(callback));

            IntPtr method = Marshal.GetFunctionPointerForDelegate<TDelegate>(callback);
            _methods.Add((callback, method));
        }

        /// <summary>
        /// Completes the VTable, registering its GUID with the associated COMCallableIUnknown's QueryInterface
        /// method.  Note that if this method is not called, then the COM interface will NOT be registered.
        /// </summary>
        /// <returns>A pointer to the interface built.  This pointer has not been AddRef'ed.</returns>
        public IntPtr Complete()
        {
            if (_complete)
                throw new InvalidOperationException();

            _complete = true;

            List<Delegate> delegates = new(_methods.Count);
            IntPtr obj = Marshal.AllocHGlobal(IntPtr.Size);

            int vtablePartSize = _methods.Count * IntPtr.Size;
            IntPtr* vtable = (IntPtr*)Marshal.AllocHGlobal(vtablePartSize + sizeof(IUnknownVTable));
            *(void**)obj = vtable;

            IUnknownVTable iunk = _wrapper.IUnknown;
            *vtable++ = new IntPtr(iunk.QueryInterface);
            *vtable++ = new IntPtr(iunk.AddRef);
            *vtable++ = new IntPtr(iunk.Release);

            foreach ((Delegate callback, IntPtr method) in _methods)
            {
                *vtable++ = method;
                delegates.Add(callback);
            }

            _wrapper.RegisterInterface(_guid, obj, delegates);
            return obj;
        }
    }
}