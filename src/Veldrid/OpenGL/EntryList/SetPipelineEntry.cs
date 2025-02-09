namespace Veldrid.OpenGL.EntryList;

internal struct SetPipelineEntry(Tracked<Pipeline> pipeline)
{
    public readonly Tracked<Pipeline> Pipeline = pipeline;
}