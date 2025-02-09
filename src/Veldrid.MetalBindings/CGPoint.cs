namespace Veldrid.MetalBindings;

public struct CGPoint(double x, double y)
{
    public CGFloat x = x;
    public CGFloat y = y;

    public override string ToString() => string.Format("({0},{1})", x, y);
}