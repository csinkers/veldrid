﻿using System.Collections.Generic;
using System.Numerics;

namespace Veldrid.NeoDemo;

internal class RenderOrderKeyComparer : IComparer<Renderable>
{
    public Vector3 CameraPosition { get; set; }

    public int Compare(Renderable? x, Renderable? y)
    {
        if (x == null)
        {
            return y == null ? 0 : -1;
        }
        if (y == null)
        {
            return 1;
        }
        return x.GetRenderOrderKey(CameraPosition).CompareTo(y.GetRenderOrderKey(CameraPosition));
    }
}
