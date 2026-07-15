namespace CC2CheatGUI.Core;

/// <summary>
/// Maps Carrier Command 2 item IDs to display names and categories.
///
/// The 0..50 names are taken verbatim from the community-maintained order used by the
/// carrier hold's positional <c>item_quantities</c> list (the same order the NerdGDev
/// CC2-Save-Editor uses). IDs 8, 58, 59, 60 and a couple of corrections come from the
/// Steam / Nexus "All Item IDs" references.
///
/// Item IDs are positional and can change between game versions, so anything not in the
/// table is rendered as "Item &lt;id&gt;" instead of being guessed.
/// </summary>
public static class ItemCatalog
{
    public const string CatAmmo = "Ammo";
    public const string CatMunitions = "Munitions";
    public const string CatTurrets = "Turrets / Weapons";
    public const string CatComponents = "Components / Utility";
    public const string CatVehicles = "Vehicles";
    public const string CatFuel = "Fuel";
    public const string CatOther = "Other";

    private static readonly Dictionary<int, string> Names = new()
    {
        [0] = "Ammo 30mm",
        [1] = "Seal Chassis",
        [2] = "Walrus Chassis",
        [3] = "Bear Chassis",
        [4] = "Albatross Chassis",
        [5] = "Manta Chassis",
        [6] = "Razorbill Chassis",
        [7] = "Petrel Chassis",
        [8] = "Barge Chassis",
        [9] = "Turret 30mm",
        [10] = "Aircraft Chaingun",
        [11] = "Rocket Pod",
        [12] = "Turret (CIWS)",
        [13] = "Turret (IR Missile)",
        [14] = "Bomb (Light)",
        [15] = "Bomb (Medium)",
        [16] = "Bomb (Heavy)",
        [17] = "Missile (IR)",
        [18] = "Missile (Laser)",
        [19] = "Missile (AA)",
        [20] = "Actuated Camera",
        // 21 intentionally omitted — unconfirmed across sources (shown as "Item 21").
        [22] = "Gimbal Camera",
        [23] = "Observation Camera",
        [24] = "Radar (AWACS)",
        [25] = "Fuel Tank (Aircraft)",
        [26] = "Flare Launcher",
        [27] = "Battle Cannon",
        [28] = "Artillery Gun",
        [29] = "Cruise Missile",
        [30] = "Rocket",
        [31] = "Ammo (Flare)",
        [32] = "Ammo (20mm)",
        [33] = "Ammo (100mm)",
        [34] = "Ammo (120mm)",
        [35] = "Ammo (160mm)",
        [36] = "Fuel (1000L)",
        [37] = "Torpedo",
        [38] = "TV-Guided Missile",
        [39] = "Torpedo (Noise)",
        [40] = "Torpedo (Countermeasure)",
        [41] = "Radar",
        [42] = "Sonic Pulse Generator",
        [43] = "Smoke Launcher (Stream)",
        [44] = "Smoke Launcher (Explosive)",
        [45] = "Ammo (Sonic Pulse)",
        [46] = "Ammo (Smoke)",
        [47] = "Turret 40mm",
        [48] = "Ammo (40mm)",
        [49] = "Heavy Cannon",
        [50] = "Virus Module",
        // Higher IDs from later game content (Steam/Nexus ID lists).
        [58] = "Mule Chassis",
        [59] = "Deployable Droid",
        [60] = "Gimbal Turret",
    };

    private static readonly Dictionary<int, string> Categories = BuildCategories();

    public static string NameOf(int id) =>
        Names.TryGetValue(id, out var name) ? name : $"Item {id}";

    public static string CategoryOf(int id) =>
        Categories.TryGetValue(id, out var cat) ? cat : CatOther;

    /// <summary>True when the ID has a confirmed display name.</summary>
    public static bool IsKnown(int id) => Names.ContainsKey(id);

    /// <summary>Human-readable vehicle type for a chassis <c>definition_index</c>.</summary>
    public static string DescribeVehicle(string definitionIndex) => definitionIndex switch
    {
        "0" => "Carrier",
        "1" => "Seal",
        "2" => "Walrus",
        "3" => "Bear",
        "4" => "Albatross",
        "5" => "Manta",
        "6" => "Razorbill",
        "7" => "Petrel",
        "8" => "Barge",
        "58" => "Mule",
        "59" => "Droid",
        "" => "Vehicle",
        _ => $"Vehicle (def {definitionIndex})",
    };

    private static Dictionary<int, string> BuildCategories()
    {
        var map = new Dictionary<int, string>();
        void Tag(string cat, params int[] ids)
        {
            foreach (var id in ids) map[id] = cat;
        }

        Tag(CatAmmo, 0, 31, 32, 33, 34, 35, 45, 46, 48);
        Tag(CatMunitions, 14, 15, 16, 17, 18, 19, 29, 30, 37, 38, 39, 40, 50);
        Tag(CatTurrets, 9, 10, 11, 12, 13, 26, 27, 28, 47, 49, 60);
        Tag(CatComponents, 20, 22, 23, 24, 25, 41, 42, 43, 44);
        Tag(CatVehicles, 1, 2, 3, 4, 5, 6, 7, 8, 58, 59);
        Tag(CatFuel, 36);
        return map;
    }
}
