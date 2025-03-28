﻿using System;

namespace Veldrid.Tests.Utilities;

internal unsafe class TextureDataReaderWriter(
    int redBits,
    int greenBits,
    int blueBits,
    int alphaBits
)
{
    public int RedBits { get; } = redBits;
    public int GreenBits { get; } = greenBits;
    public int BlueBits { get; } = blueBits;
    public int AlphaBits { get; } = alphaBits;
    public int PixelBytes { get; } = (redBits + blueBits + greenBits + alphaBits) / 8;

    public ulong RMaxValue => (ulong)Math.Pow(2, RedBits) - 1;
    public ulong GMaxValue => (ulong)Math.Pow(2, GreenBits) - 1;
    public ulong BMaxValue => (ulong)Math.Pow(2, BlueBits) - 1;
    public ulong AMaxValue => (ulong)Math.Pow(2, AlphaBits) - 1;

    public WidePixel ReadPixel(byte* pixelPtr)
    {
        ulong? r = ReadBits(pixelPtr, 0, RedBits);
        ulong? g = ReadBits(pixelPtr, RedBits, GreenBits);
        ulong? b = ReadBits(pixelPtr, RedBits + GreenBits, BlueBits);
        ulong? a = ReadBits(pixelPtr, RedBits + GreenBits + BlueBits, AlphaBits);

        return new(r, g, b, a);
    }

    ulong? ReadBits(byte* pixelPtr, int bitOffset, int numBits)
    {
        if (numBits == 0)
        {
            return null;
        }

        ulong ret = 0;

        for (int i = 0; i < numBits; i++)
        {
            if (IsBitSet(pixelPtr, bitOffset + i))
            {
                SetBit((byte*)&ret, i);
            }
        }

        return ret;
    }

    public void WritePixel(byte* pixelPtr, WidePixel pixel)
    {
        WriteBits(pixel.R, pixelPtr, 0, RedBits);
        WriteBits(pixel.G, pixelPtr, RedBits, GreenBits);
        WriteBits(pixel.B, pixelPtr, RedBits + GreenBits, BlueBits);
        WriteBits(pixel.A, pixelPtr, RedBits + GreenBits + BlueBits, AlphaBits);
    }

    internal void WriteBits(ulong? value, byte* basePtr, int bitOffset, int numBits)
    {
        if (value == null)
        {
            return;
        }

        ulong val = value.Value;

        for (int i = 0; i < numBits; i++)
        {
            if (IsBitSet((byte*)&val, i))
            {
                SetBit(basePtr, bitOffset + i);
            }
        }
    }

    internal byte[] GetDataArray(uint srcWidth, uint srcHeight, uint srcDepth)
    {
        return new byte[PixelBytes * srcWidth * srcHeight * srcDepth];
    }

    internal WidePixel GetTestPixel(uint x, uint y, uint z)
    {
        ulong? r = x % RMaxValue;
        ulong? g = GreenBits != 0 ? (y % GMaxValue) : null;
        ulong? b = BlueBits != 0 ? (z % BMaxValue) : null;
        ulong? a = AlphaBits != 0 ? 1 : null;
        return new(r, g, b, a);
    }

    bool IsBitSet(byte* basePtr, int bit)
    {
        int index = Math.DivRem(bit, 8, out int remainder);
        byte val = basePtr[index];
        ulong mask = 1ul << remainder;
        return (val & mask) != 0;
    }

    void SetBit(byte* basePtr, int bit)
    {
        int index = Math.DivRem(bit, 8, out int remainder);
        byte val = basePtr[index];
        byte mask = (byte)(1 << remainder);
        byte newVal = (byte)(val | mask);
        basePtr[index] = newVal;
    }
}

internal struct WidePixel(ulong? r, ulong? g, ulong? b, ulong? a) : IEquatable<WidePixel>
{
    public readonly ulong? R = r;
    public readonly ulong? G = g;
    public readonly ulong? B = b;
    public readonly ulong? A = a;

    public bool Equals(WidePixel other)
    {
        return R.HasValue == other.R.HasValue
            && R.GetValueOrDefault().Equals(other.R.GetValueOrDefault())
            && G.HasValue == other.G.HasValue
            && G.GetValueOrDefault().Equals(other.G.GetValueOrDefault())
            && B.HasValue == other.B.HasValue
            && B.GetValueOrDefault().Equals(other.B.GetValueOrDefault())
            && A.HasValue == other.A.HasValue
            && A.GetValueOrDefault().Equals(other.A.GetValueOrDefault());
    }

    public override string ToString()
    {
        return $"{R}, {G}, {B}, {A}";
    }
}
