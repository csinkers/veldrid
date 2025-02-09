namespace Veldrid.MetalBindings;

public struct CGRect(CGPoint origin, CGSize size)
{
    public CGPoint origin = origin;
    public CGSize size = size;

    public override string ToString() => string.Format("{0}, {1}", origin, size);
}