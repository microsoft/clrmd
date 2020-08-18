// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal sealed class ObjectPool<T>
        where T : new()
    {
        private readonly Queue<T> _bag = new Queue<T>();
        private readonly Action<ObjectPool<T>, T> _setOwner;

        public ObjectPool(Action<ObjectPool<T>, T> setOwner)
        {
            _setOwner = setOwner;
        }

        public T Rent()
        {
            lock (_bag)
                if (_bag.Count > 0)
                    return _bag.Dequeue();

            T result = new T();
            _setOwner(this, result);
            return result;
        }

        public void Return(T t)
        {
            lock (_bag)
                _bag.Enqueue(t);
        }
    }
}
