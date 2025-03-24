namespace Veldrid.MetalBindings;

internal struct MTLViewport(
    double originX,
    double originY,
    double width,
    double height,
    double znear,
    double zfar
)
{
    public double originX = originX;
    public double originY = originY;
    public double width = width;
    public double height = height;
    public double znear = znear;
    public double zfar = zfar;
}
