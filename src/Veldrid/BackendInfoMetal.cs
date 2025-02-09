#if !EXCLUDE_METAL_BACKEND
using System.Collections.ObjectModel;
using System.Linq;
using Veldrid.MetalBindings;
using Veldrid.MTL;

namespace Veldrid;

/// <summary>
/// Exposes Metal-specific functionality,
/// useful for interoperating with native components which interface directly with Metal.
/// Can only be used on <see cref="GraphicsBackend.Metal"/>.
/// </summary>
public class BackendInfoMetal
{
    readonly MTLGraphicsDevice _gd;

    internal BackendInfoMetal(MTLGraphicsDevice gd)
    {
        _gd = gd;
        FeatureSet = new(_gd.MetalFeatures.ToArray());
    }

    /// <summary>
    /// The list of all feature sets supported by the Metal device.
    /// </summary>
    public ReadOnlyCollection<MTLFeatureSet> FeatureSet { get; }

    /// <summary>
    /// The maximum feature set supported by the Metal device.
    /// </summary>
    public MTLFeatureSet MaxFeatureSet => _gd.MetalFeatures.MaxFeatureSet;
}
#endif
