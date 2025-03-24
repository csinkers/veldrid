using System.Runtime.InteropServices;

namespace Veldrid.MetalBindings;

[StructLayout(LayoutKind.Sequential)]
internal struct MTLClearColor(double r, double g, double b, double a)
{
    public double red = r;
    public double green = g;
    public double blue = b;
    public double alpha = a;
}
