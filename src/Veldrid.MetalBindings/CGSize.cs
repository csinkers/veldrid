namespace Veldrid.MetalBindings;

public struct CGSize(double width, double height)
{
    public double width = width;
    public double height = height;

    public override string ToString() => string.Format("{0} x {1}", width, height);
}
