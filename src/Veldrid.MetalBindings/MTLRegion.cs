namespace Veldrid.MetalBindings;

public struct MTLRegion(MTLOrigin origin, MTLSize size)
{
    public MTLOrigin origin = origin;
    public MTLSize size = size;
}
