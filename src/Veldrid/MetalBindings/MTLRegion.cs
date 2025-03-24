namespace Veldrid.MetalBindings;

internal struct MTLRegion(MTLOrigin origin, MTLSize size)
{
    public MTLOrigin origin = origin;
    public MTLSize size = size;
}
