using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugins;

internal static class EmbeddedManifest
{
    internal static PluginSpec Read(string path)
    {
        using var file = File.OpenRead(path);
        using var image = new PEReader(file);
        if (!image.HasMetadata)
            throw new JsonException("The selected DLL has no managed assembly metadata.");
        var metadata = image.GetMetadataReader();
        foreach (var handle in metadata.ManifestResources)
        {
            var resource = metadata.GetManifestResource(handle);
            if (
                !resource.Implementation.IsNil
                || !metadata.StringComparer.Equals(resource.Name, "LazyTuiHub.plugin.json")
            )
                continue;
            var section = image.GetSectionData(image.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress);
            var offset = checked((int)resource.Offset);
            if (offset > section.Length - sizeof(int))
                throw new JsonException("Invalid manifest resource offset.");
            var reader = section.GetReader(offset, section.Length - offset);
            var length = reader.ReadInt32();
            if (length < 0 || length > reader.RemainingBytes)
                throw new JsonException("Invalid manifest resource length.");
            return JsonSerializer.Deserialize<PluginSpec>(reader.ReadBytes(length), PipelineStore.JsonOptions)
                ?? throw new JsonException("Empty plugin declaration.");
        }
        throw new JsonException("The assembly has no embedded LazyTuiHub plugin declaration.");
    }
}
