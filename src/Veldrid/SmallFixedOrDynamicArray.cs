using System;
using System.Buffers;

namespace Veldrid;

internal unsafe struct SmallFixedOrDynamicArray : IDisposable
{
    public const int MaxFixedValues = 5;

    static readonly ArrayPool<uint> _arrayPool = ArrayPool<uint>.Create();

    public readonly uint Count;
    fixed uint FixedData[MaxFixedValues];
    public uint[]? Data;

    public uint Get(uint i) => Count > MaxFixedValues ? Data![i] : FixedData[i];

    public SmallFixedOrDynamicArray(ReadOnlySpan<uint> offsets)
    {
        if (offsets.Length > MaxFixedValues)
        {
            Data = _arrayPool.Rent(offsets.Length);
        }
        else
        {
            for (int i = 0; i < offsets.Length; i++)
            {
                FixedData[i] = offsets[i];
            }
            Data = null;
        }

        Count = (uint)offsets.Length;
    }

    public void Dispose()
    {
        if (Data != null)
        {
            _arrayPool.Return(Data);
            Data = null;
        }
    }
}
