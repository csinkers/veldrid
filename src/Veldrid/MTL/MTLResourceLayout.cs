using Veldrid.SPIRV;

namespace Veldrid.MTL;

internal sealed class MTLResourceLayout : ResourceLayout
{
    readonly ResourceBindingInfo[] _bindingInfosByVdIndex;
    bool _disposed;
    public uint BufferCount { get; }
    public uint TextureCount { get; }
    public uint SamplerCount { get; }
#if !VALIDATE_USAGE
    public ResourceKind[] ResourceKinds { get; }
#endif

    public ResourceBindingInfo GetBindingInfo(int index) => _bindingInfosByVdIndex[index];

#if !VALIDATE_USAGE
    public ResourceLayoutDescription Description { get; }
#endif

    public MTLResourceLayout(in ResourceLayoutDescription description)
        : base(description)
    {
#if !VALIDATE_USAGE
        Description = description;
#endif

        ResourceLayoutElementDescription[] elements = description.Elements;
#if !VALIDATE_USAGE
        ResourceKinds = new ResourceKind[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        {
            ResourceKinds[i] = elements[i].Kind;
        }
#endif

        _bindingInfosByVdIndex = new ResourceBindingInfo[elements.Length];

        uint bufferIndex = 0;
        uint texIndex = 0;
        uint samplerIndex = 0;

        for (int i = 0; i < _bindingInfosByVdIndex.Length; i++)
        {
            uint slot = elements[i].Kind switch
            {
                ResourceKind.UniformBuffer => bufferIndex++,
                ResourceKind.StructuredBufferReadOnly => bufferIndex++,
                ResourceKind.StructuredBufferReadWrite => bufferIndex++,
                ResourceKind.TextureReadOnly => texIndex++,
                ResourceKind.TextureReadWrite => texIndex++,
                ResourceKind.Sampler => samplerIndex++,
                _ => Illegal.Value<ResourceKind, uint>(),
            };

            _bindingInfosByVdIndex[i] = new(
                slot,
                elements[i].Stages,
                elements[i].Kind,
                (elements[i].Options & ResourceLayoutElementOptions.DynamicBinding) != 0
            );
        }

        BufferCount = bufferIndex;
        TextureCount = texIndex;
        SamplerCount = samplerIndex;
    }

    public override string? Name { get; set; }

    public override bool IsDisposed => _disposed;

    public override void Dispose()
    {
        _disposed = true;
    }

    internal struct ResourceBindingInfo(
        uint slot,
        ShaderStages stages,
        ResourceKind kind,
        bool dynamicBuffer
    )
    {
        public readonly uint Slot = slot;
        public readonly ShaderStages Stages = stages;
        public readonly ResourceKind Kind = kind;
        public readonly bool DynamicBuffer = dynamicBuffer;
    }
}
