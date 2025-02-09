namespace Veldrid.Vk;

internal struct DescriptorResourceCounts(
    uint uniformBufferCount,
    uint uniformBufferDynamicCount,
    uint sampledImageCount,
    uint samplerCount,
    uint storageBufferCount,
    uint storageBufferDynamicCount,
    uint storageImageCount
)
{
    public readonly uint UniformBufferCount = uniformBufferCount;
    public readonly uint SampledImageCount = sampledImageCount;
    public readonly uint SamplerCount = samplerCount;
    public readonly uint StorageBufferCount = storageBufferCount;
    public readonly uint StorageImageCount = storageImageCount;
    public readonly uint UniformBufferDynamicCount = uniformBufferDynamicCount;
    public readonly uint StorageBufferDynamicCount = storageBufferDynamicCount;
}
