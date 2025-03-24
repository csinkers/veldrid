namespace Veldrid.MetalBindings;

internal struct CGSize(double width, double height)
{
    public readonly double width = width;
    public readonly double height = height;

    public override string ToString() => string.Format("{0} x {1}", width, height);
}
