﻿using System;
using System.IO;

namespace Veldrid.Tests.Utilities;

internal class FileShaderProvider(string directory) : IShaderProvider
{
    readonly string _directory = directory ?? throw new ArgumentNullException(nameof(directory));

    public string GetPath(string name)
    {
        return Path.Combine(_directory, "Shaders", name);
    }

    public Stream OpenRead(string path)
    {
        return File.OpenRead(path);
    }

    public byte[] ReadAllBytes(string path)
    {
        return File.ReadAllBytes(path);
    }
}
