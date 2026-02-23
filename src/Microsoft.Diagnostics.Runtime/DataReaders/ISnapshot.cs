// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.DataReaders;

internal interface ISnapshot
{
    IDataReader DataReader { get; }
    void SaveSnapshot(string path, bool overwrite);
}
