﻿using System;
using System.Collections.Generic;

namespace Veldrid.Utilities;

public class DisposeCollector
{
    readonly List<IDisposable> _disposables = [];

    public void Add(IDisposable disposable)
    {
        _disposables.Add(disposable);
    }

    public void Add(IDisposable first, IDisposable second)
    {
        _disposables.Add(first);
        _disposables.Add(second);
    }

    public void Add(IDisposable first, IDisposable second, IDisposable third)
    {
        _disposables.Add(first);
        _disposables.Add(second);
        _disposables.Add(third);
    }

    public void Add(IDisposable first, IDisposable second, IDisposable third, IDisposable fourth)
    {
        _disposables.Add(first);
        _disposables.Add(second);
        _disposables.Add(third);
        _disposables.Add(fourth);
    }

    public void Add<T>(params IEnumerable<T> array)
        where T : IDisposable
    {
        foreach (T item in array)
            _disposables.Add(item);
    }

    public void Remove(IDisposable disposable)
    {
        if (!_disposables.Remove(disposable))
        {
            throw new InvalidOperationException(
                "Unable to untrack " + disposable + ". It was not previously tracked."
            );
        }
    }

    public void DisposeAll()
    {
        foreach (IDisposable disposable in _disposables)
            disposable.Dispose();

        _disposables.Clear();
    }
}
