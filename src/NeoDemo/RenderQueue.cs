﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Veldrid.NeoDemo;

public class RenderQueue : IEnumerable<Renderable>
{
    const int DefaultCapacity = 250;

    readonly List<RenderItemIndex> _indices = new(DefaultCapacity);
    readonly List<Renderable> _renderables = new(DefaultCapacity);

    public int Count => _renderables.Count;

    public void Clear()
    {
        _indices.Clear();
        _renderables.Clear();
    }

    public void AddRange(List<Renderable> Renderables, Vector3 viewPosition)
    {
        for (int i = 0; i < Renderables.Count; i++)
        {
            Renderable Renderable = Renderables[i];
            if (Renderable != null)
            {
                Add(Renderable, viewPosition);
            }
        }
    }

    public void AddRange(IReadOnlyList<Renderable> Renderables, Vector3 viewPosition)
    {
        for (int i = 0; i < Renderables.Count; i++)
        {
            Renderable Renderable = Renderables[i];
            if (Renderable != null)
            {
                Add(Renderable, viewPosition);
            }
        }
    }

    public void AddRange(IEnumerable<Renderable> Renderables, Vector3 viewPosition)
    {
        foreach (Renderable item in Renderables)
        {
            if (item != null)
            {
                Add(item, viewPosition);
            }
        }
    }

    public void Add(Renderable item, Vector3 viewPosition)
    {
        int index = _renderables.Count;
        _indices.Add(new RenderItemIndex(item.GetRenderOrderKey(viewPosition), index));
        _renderables.Add(item);
        Debug.Assert(_renderables.IndexOf(item) == index);
    }

    public void Sort()
    {
        _indices.Sort();
    }

    public void Sort(Comparer<RenderOrderKey> keyComparer)
    {
        _indices.Sort(
            (RenderItemIndex first, RenderItemIndex second) =>
                keyComparer.Compare(first.Key, second.Key)
        );
    }

    public void Sort(Comparer<RenderItemIndex> comparer)
    {
        _indices.Sort(comparer);
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(_indices, _renderables);
    }

    IEnumerator<Renderable> IEnumerable<Renderable>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator(List<RenderItemIndex> indices, List<Renderable> renderables)
        : IEnumerator<Renderable>
    {
        int _nextItemIndex = 0;
        Renderable _currentItem = null;

        public Renderable Current => _currentItem;
        object IEnumerator.Current => _currentItem;

        public void Dispose() { }

        public bool MoveNext()
        {
            if (_nextItemIndex >= indices.Count)
            {
                _currentItem = null;
                return false;
            }
            else
            {
                RenderItemIndex currentIndex = indices[_nextItemIndex];
                _currentItem = renderables[currentIndex.ItemIndex];
                _nextItemIndex += 1;
                return true;
            }
        }

        public void Reset()
        {
            _nextItemIndex = 0;
            _currentItem = null;
        }
    }
}
