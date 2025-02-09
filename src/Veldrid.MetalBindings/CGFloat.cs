namespace Veldrid.MetalBindings;

// TODO: Technically this should be "pointer-sized",
// but there are no non-64-bit platforms that anyone cares about.
public struct CGFloat(double value)
{
    public double Value
    {
        get => value;
    }

    public static implicit operator CGFloat(double value) => new(value);

    public static implicit operator double(CGFloat cgf) => cgf.Value;

    public override string ToString() => value.ToString();
}
