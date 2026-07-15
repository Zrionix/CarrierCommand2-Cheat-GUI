using System.Globalization;
using System.Xml;

namespace CC2CheatGUI.Core;

/// <summary>
/// An island tile in the world. Ownership is the <c>team_control</c> attribute
/// (0 = neutral, 1 = player, 2+ = enemy factions). Setting it hands the island — its facility,
/// garrison and blueprint reward — to that team.
/// </summary>
public sealed class IslandTile
{
    private readonly XmlElement _tile;

    internal IslandTile(XmlElement tile)
    {
        _tile = tile;
        Id = tile.GetAttribute("id");
        BiomeType = tile.GetAttribute("biome_type");
    }

    public string Id { get; }
    public string BiomeType { get; }

    public int TeamControl
    {
        get => int.TryParse(_tile.GetAttribute("team_control"), out var v) ? v : 0;
        set
        {
            _tile.SetAttribute("team_control", value.ToString(CultureInfo.InvariantCulture));
            // Clear any in-progress capture so ownership sticks.
            if (_tile.HasAttribute("team_capture")) _tile.SetAttribute("team_capture", "4294967295");
            if (_tile.HasAttribute("team_capture_progress")) _tile.SetAttribute("team_capture_progress", "0.00000000e+00");
        }
    }

    public string BiomeName => BiomeType switch
    {
        "0" => "Tundra",
        "1" => "Desert",
        "2" => "Temperate",
        "3" => "Tropical",
        "4" => "Arctic",
        "5" => "Volcanic",
        "6" => "Rocky",
        "7" => "Wetland",
        _ => $"Biome {BiomeType}",
    };
}
