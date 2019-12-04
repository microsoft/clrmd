// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal sealed class ObjectPool<T>
    {
        private readonly ConcurrentBag<T> _bag = new ConcurrentBag<T>();
        private readonly Func<T> _generator;
        private readonly Action<ObjectPool<T>, T> _setOwner;

        public ObjectPool(Func<T> generator, Action<ObjectPool<T>, T> setOwner)
        {
            _generator = generator;
            _setOwner = setOwner;
        }

        public T Rent()
        {
            if (!_bag.TryTake(out T result))
                result = _generator();

            _setOwner(this, result);
            return result;
        }

        public void Return(T t)
        {
            _bag.Add(t);
        }
    }
}
