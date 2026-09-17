using Dalamud.Configuration;

namespace AoAoEnergy;

internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public OffsetProfileStore OffsetProfiles { get; set; } = new();
}
