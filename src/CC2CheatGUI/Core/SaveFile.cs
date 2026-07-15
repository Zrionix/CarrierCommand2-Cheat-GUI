using System.Globalization;
using System.Xml;

namespace CC2CheatGUI.Core;

/// <summary>A single located/edited Carrier Command 2 save.xml.</summary>
public sealed class SaveFile
{
    private readonly Cc2Document _doc;

    public string Path { get; }
    public IReadOnlyList<InventoryContainer> Containers { get; private set; } = Array.Empty<InventoryContainer>();

    /// <summary>The player's carrier hold container (used to fingerprint the hold in live memory), or null.</summary>
    public InventoryContainer? PlayerCarrierHold =>
        Containers.FirstOrDefault(c => c.Kind == ContainerKind.VehicleHold
                                       && c.Label.Contains("Carrier") && c.Label.Contains("YOURS"))
        ?? Containers.FirstOrDefault(c => c.Kind == ContainerKind.VehicleHold && c.Label.Contains("Carrier"));

    /// <summary>The positional quantity row (index = item id) of a vehicle-hold container.</summary>
    public static int[] RowOf(InventoryContainer hold) =>
        hold.Entries.OrderBy(e => e.ItemId).Select(e => (int)e.Quantity).ToArray();

    /// <summary>Every team in the save, with directly-editable currency &amp; blueprints.</summary>
    public IReadOnlyList<TeamInfo> Teams { get; private set; } = Array.Empty<TeamInfo>();

    /// <summary>Every parsed live vehicle state (carrier + deployed units of all teams).</summary>
    public IReadOnlyList<VehicleState> VehicleStates { get; private set; } = Array.Empty<VehicleState>();

    /// <summary>Every island tile, with editable ownership.</summary>
    public IReadOnlyList<IslandTile> Islands { get; private set; } = Array.Empty<IslandTile>();

    /// <summary>The non-AI team id, when one could be identified ("" otherwise).</summary>
    public string PlayerTeamId { get; private set; } = "";

    /// <summary>The player's team, if identified.</summary>
    public TeamInfo? PlayerTeam => Teams.FirstOrDefault(t => t.IsPlayer);

    private SaveFile(string path, Cc2Document doc)
    {
        Path = path;
        _doc = doc;
    }

    public static SaveFile Load(string path)
    {
        var doc = Cc2Document.Load(path);
        var save = new SaveFile(path, doc);
        save.Discover();
        return save;
    }

    // ---------------------------------------------------------------------
    // Discovery
    // ---------------------------------------------------------------------

    private void Discover()
    {
        DiscoverTeams();
        DiscoverVehicleStates();
        DiscoverIslands();

        var containers = new List<InventoryContainer>();
        containers.AddRange(BuildHoldContainers());
        containers.AddRange(DiscoverIslandStock());
        Containers = containers;
    }

    private void DiscoverIslands()
    {
        var list = new List<IslandTile>();
        // Island tiles carry biome_type + team_control and (unlike team nodes) no is_ai_controlled.
        var nodes = _doc.SelectNodes("//tiles/tiles/t[@team_control][@biome_type]");
        if (nodes != null)
            foreach (XmlNode n in nodes)
                if (n is XmlElement el) list.Add(new IslandTile(el));
        Islands = list;
    }

    /// <summary>
    /// CC2 stores each faction as a <c>&lt;t&gt;</c> node carrying <c>is_ai_controlled</c> and a
    /// direct <c>currency</c> attribute. The player is the (first) non-AI team.
    /// </summary>
    private void DiscoverTeams()
    {
        var teams = new List<TeamInfo>();
        var nodes = _doc.SelectNodes("//*[@is_ai_controlled][@currency]");
        if (nodes != null)
        {
            foreach (XmlNode node in nodes)
            {
                if (node is not XmlElement el) continue;
                var attr = el.GetAttributeNode("currency");
                if (attr == null) continue;

                var ai = el.GetAttribute("is_ai_controlled").Trim().ToLowerInvariant();
                bool isAi = ai is "true" or "1";

                var id = el.GetAttribute("id");
                if (id.Length == 0) id = el.GetAttribute("team_id");

                teams.Add(new TeamInfo(el, attr)
                {
                    Id = id,
                    IsAi = isAi,
                    IsNeutral = el.GetAttribute("is_neutral").Trim().ToLowerInvariant() is "true" or "1",
                    IsDestroyed = el.GetAttribute("is_destroyed").Trim().ToLowerInvariant() is "true" or "1",
                });
            }
        }
        Teams = teams;

        var player = teams.FirstOrDefault(t => !t.IsAi && !t.IsNeutral)
                     ?? teams.FirstOrDefault(t => !t.IsAi);
        if (player != null) player.IsPlayer = true;
        PlayerTeamId = player?.Id ?? "";
    }

    /// <summary>Type + definition_index for a vehicle id, taken from the top-level vehicle roster.</summary>
    private Dictionary<string, (string Team, string Def)> _vehicleRoster = new();

    private void DiscoverVehicleStates()
    {
        // Roster: vehicle id -> (team_id, definition_index), from the plain <vehicles> list.
        _vehicleRoster = new Dictionary<string, (string, string)>();
        var vehicleNodes = _doc.SelectNodes("//*[@definition_index][@team_id]");
        if (vehicleNodes != null)
            foreach (XmlNode node in vehicleNodes)
            {
                if (node is not XmlElement el) continue;
                var id = el.GetAttribute("id");
                if (id.Length == 0 || _vehicleRoster.ContainsKey(id)) continue;
                _vehicleRoster[id] = (el.GetAttribute("team_id"), el.GetAttribute("definition_index"));
            }

        var states = new List<VehicleState>();
        var stateNodes = _doc.SelectNodes("//vehicle_states/v[@state]")
                         ?? _doc.SelectNodes("//*[@state]");
        if (stateNodes != null)
            foreach (XmlNode node in stateNodes)
            {
                if (node is not XmlElement el) continue;
                var vs = VehicleState.TryCreate(el);
                if (vs != null) states.Add(vs);
            }
        VehicleStates = states;
    }

    /// <summary>True when a vehicle state belongs to the player's team.</summary>
    public bool IsPlayerUnit(VehicleState vs)
    {
        var team = vs.TeamId;
        if (team.Length == 0 && _vehicleRoster.TryGetValue(vs.Id, out var info)) team = info.Team;
        return PlayerTeamId.Length > 0 && team == PlayerTeamId;
    }

    /// <summary>Human label for a vehicle state (type + team + id), player-flagged.</summary>
    public string DescribeUnit(VehicleState vs)
    {
        var team = vs.TeamId;
        var def = vs.DefinitionIndex;
        if ((team.Length == 0 || def.Length == 0) && _vehicleRoster.TryGetValue(vs.Id, out var info))
        {
            if (team.Length == 0) team = info.Team;
            if (def.Length == 0) def = info.Def;
        }
        string type = ItemCatalog.DescribeVehicle(def);
        string label = $"{type} — team {team} (vehicle {vs.Id})";
        return IsPlayerUnit(vs) ? "★ " + label + "  [YOURS]" : label;
    }

    private List<InventoryContainer> BuildHoldContainers()
    {
        var result = new List<InventoryContainer>();
        foreach (var vs in VehicleStates)
        {
            var hold = VehicleHoldContainer.TryCreate(vs, DescribeUnit(vs));
            if (hold != null) result.Add(hold);
        }
        // Player's holds first, then the rest, each group kept in document order.
        result.Sort((a, b) =>
        {
            int ay = a.Label.StartsWith("★") ? 0 : 1;
            int by = b.Label.StartsWith("★") ? 0 : 1;
            return ay.CompareTo(by);
        });
        return result;
    }

    // ---------------------------------------------------------------------
    // Bulk "cheat" operations
    // ---------------------------------------------------------------------

    /// <summary>Player vehicle states that carry live hitpoints/fuel/ammo.</summary>
    public IEnumerable<VehicleState> PlayerUnits => VehicleStates.Where(IsPlayerUnit);

    /// <summary>Repair + refuel + rearm every player unit. Returns the number of units affected.</summary>
    public int BuffPlayerFleet(long hitpoints = 100000, double fuel = 99999, long ammo = 9999,
        bool repair = true, bool refuel = true, bool rearm = true)
    {
        int n = 0;
        foreach (var vs in PlayerUnits)
        {
            bool touched = false;
            if (repair && vs.HasHitpoints) { vs.Hitpoints = hitpoints; touched = true; }
            if (refuel && vs.HasFuel) { vs.Fuel = fuel; touched = true; }
            if (rearm)
                foreach (var a in vs.Attachments)
                    if (a.HasAmmo) { a.Ammo = ammo; touched = true; }
            if (touched) n++;
        }
        return n;
    }

    /// <summary>Fill every positional slot of the player's carrier hold (and other player holds).</summary>
    public int FillPlayerHolds(long quantity = 999)
    {
        int n = 0;
        foreach (var vs in PlayerUnits)
            if (vs.HasInventory)
            {
                for (int i = 0; i < vs.SlotCount; i++) vs.SetItem(i, quantity);
                n++;
            }
        return n;
    }

    /// <summary>Give every island to the player. Returns count changed.</summary>
    public int OwnAllIslands()
    {
        if (PlayerTeamId.Length == 0 || !int.TryParse(PlayerTeamId, out var team)) return 0;
        int n = 0;
        foreach (var isl in Islands)
            if (isl.TeamControl != team) { isl.TeamControl = team; n++; }
        return n;
    }

    private List<InventoryContainer> DiscoverIslandStock()
    {
        var result = new List<InventoryContainer>();
        var qNodes = _doc.SelectNodes("//q[@i][@q]");
        if (qNodes == null) return result;

        // Group <q> elements by their owning parent element.
        var groups = new Dictionary<XmlElement, List<XmlElement>>();
        foreach (XmlNode node in qNodes)
        {
            if (node is not XmlElement q) continue;
            if (q.ParentNode is not XmlElement parent) continue;
            if (!groups.TryGetValue(parent, out var list))
            {
                list = new List<XmlElement>();
                groups[parent] = list;
            }
            list.Add(q);
        }

        int index = 0;
        foreach (var kv in groups)
        {
            index++;
            var label = DescribeStockOwner(kv.Key, index);
            result.Add(IslandStockContainer.Create(kv.Key, kv.Value, label));
        }
        return result;
    }

    private string DescribeStockOwner(XmlElement parent, int index)
    {
        // Climb ancestors looking for something human-meaningful.
        XmlElement? cur = parent;
        string? name = null;
        string? team = null;
        while (cur != null)
        {
            if (name == null)
            {
                var n = cur.GetAttribute("name");
                if (n.Length > 0) name = n;
            }
            if (team == null)
            {
                var t = cur.GetAttribute("team_control");
                if (t.Length > 0) team = t;
            }
            cur = cur.ParentNode as XmlElement;
        }

        var parts = new List<string> { $"Warehouse #{index}" };
        if (name != null) parts.Add($"\"{name}\"");
        if (team != null)
        {
            bool yours = PlayerTeamId.Length > 0 && team == PlayerTeamId;
            parts.Add(yours ? $"team {team} [YOURS]" : $"team {team}");
        }
        return string.Join(" — ", parts);
    }

    // ---------------------------------------------------------------------
    // Currency (value-search fallback, kept from v1)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Find every place in the save that holds the exact integer <paramref name="value"/>. Used as a
    /// fallback when the direct team currency field isn't what the user wants to edit.
    /// </summary>
    public List<CurrencyMatch> FindValue(long value)
    {
        var matches = new List<CurrencyMatch>();
        WalkForValue(_doc.Root, value, matches);
        matches.Sort((a, b) => b.Score.CompareTo(a.Score));
        return matches;
    }

    private void WalkForValue(XmlElement? element, long value, List<CurrencyMatch> sink)
    {
        if (element == null) return;

        foreach (XmlAttribute attr in element.Attributes)
        {
            if (NumericEquals(attr.Value, value))
            {
                sink.Add(new CurrencyMatch
                {
                    Attr = attr,
                    Value = value,
                    Description = $"<{element.Name} {attr.Name}=\"{attr.Value}\">",
                    Score = ScoreLocation(element, attr.Name),
                });
            }
        }

        bool hasChildElements = false;
        foreach (XmlNode child in element.ChildNodes)
        {
            if (child is XmlElement childEl)
            {
                hasChildElements = true;
                WalkForValue(childEl, value, sink);
            }
        }

        if (!hasChildElements && NumericEquals(element.InnerText, value))
        {
            sink.Add(new CurrencyMatch
            {
                Element = element,
                Value = value,
                Description = $"<{element.Name}>{element.InnerText.Trim()}</{element.Name}>",
                Score = ScoreLocation(element, element.Name),
            });
        }
    }

    private int ScoreLocation(XmlElement element, string fieldName)
    {
        int score = 0;
        var field = fieldName.ToLowerInvariant();
        if (field.Contains("currency") || field.Contains("money") ||
            field.Contains("budget") || field.Contains("funds") || field.Contains("cash"))
        {
            score += 100;
        }

        XmlElement? cur = element;
        while (cur != null)
        {
            var n = cur.Name.ToLowerInvariant();
            if (n.Contains("currency") || n.Contains("money") || n.Contains("budget")) score += 40;
            if (cur.HasAttribute("is_ai_controlled"))
            {
                var ai = cur.GetAttribute("is_ai_controlled").Trim().ToLowerInvariant();
                score += (ai is "true" or "1") ? 5 : 25; // player team weighted higher
            }
            cur = cur.ParentNode as XmlElement;
        }
        return score;
    }

    private static bool NumericEquals(string raw, long value)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        raw = raw.Trim();
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
            return l == value;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d == value && Math.Floor(d) == d;
        return false;
    }

    // ---------------------------------------------------------------------
    // Save
    // ---------------------------------------------------------------------

    /// <summary>Flush pending edits, back up the original, and write the file.</summary>
    /// <returns>The path of the backup that was written.</returns>
    public string Save()
    {
        // Vehicle/attachment edits (inventory, hitpoints, fuel, ammo) all live in escaped state blobs;
        // flush each parsed state exactly once. Island/currency/blueprint edits are already applied
        // directly to the outer document.
        foreach (var vs in VehicleStates)
            vs.Flush();

        string backup = BackupOriginal();
        _doc.Save(Path);
        return backup;
    }

    private string BackupOriginal()
    {
        // Preserve the pristine original once, and a timestamped snapshot every save.
        var pristine = Path + ".bak";
        if (!File.Exists(pristine) && File.Exists(Path))
            File.Copy(Path, pristine);

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var snapshot = $"{Path}.{stamp}.bak";
        if (File.Exists(Path))
            File.Copy(Path, snapshot, overwrite: true);
        return snapshot;
    }
}

/// <summary>A team/faction node in the save, exposing its directly-editable currency.</summary>
public sealed class TeamInfo
{
    private readonly XmlElement _element;
    private readonly XmlAttribute _currencyAttr;

    internal TeamInfo(XmlElement element, XmlAttribute currencyAttr)
    {
        _element = element;
        _currencyAttr = currencyAttr;
    }

    public string Id { get; init; } = "";
    public bool IsAi { get; init; }
    public bool IsNeutral { get; init; }
    public bool IsDestroyed { get; init; }
    public bool IsPlayer { get; set; }

    public long Currency
    {
        get => long.TryParse(_currencyAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        set => _currencyAttr.Value = value.ToString(CultureInfo.InvariantCulture);
    }

    // ----- blueprints (tech unlocks) -----
    // Stored as <unlocked_blueprints><bytes size="N"><b value="0..255"/>...</bytes></unlocked_blueprints>,
    // a little-endian bit array: byte i, bit j => blueprint (i*8 + j) is unlocked.

    /// <summary>Count of currently-unlocked blueprints (popcount of the bit array).</summary>
    public int UnlockedBlueprintCount
    {
        get
        {
            int count = 0;
            var bytes = _element["unlocked_blueprints"]?["bytes"];
            if (bytes != null)
                foreach (XmlNode c in bytes.ChildNodes)
                    if (c is XmlElement b && b.Name == "b" && int.TryParse(b.GetAttribute("value"), out var v))
                        count += System.Numerics.BitOperations.PopCount((uint)(byte)v);
            return count;
        }
    }

    /// <summary>Unlock every blueprint by filling the bit array with 0xFF bytes.</summary>
    public void UnlockAllBlueprints(int byteCount = 8)
    {
        var bytes = EnsureBytesElement();
        bytes.SetAttribute("size", byteCount.ToString(CultureInfo.InvariantCulture));
        bytes.IsEmpty = false;
        while (bytes.HasChildNodes) bytes.RemoveChild(bytes.FirstChild!);
        var doc = _element.OwnerDocument!;
        for (int i = 0; i < byteCount; i++)
        {
            var b = doc.CreateElement("b");
            b.SetAttribute("value", "255");
            bytes.AppendChild(b);
        }
    }

    /// <summary>Clear all unlocked blueprints (reset the bit array to empty).</summary>
    public void ClearBlueprints()
    {
        var bytes = EnsureBytesElement();
        bytes.SetAttribute("size", "0");
        while (bytes.HasChildNodes) bytes.RemoveChild(bytes.FirstChild!);
        bytes.IsEmpty = true;
    }

    private XmlElement EnsureBytesElement()
    {
        var doc = _element.OwnerDocument!;
        var unlocked = _element["unlocked_blueprints"];
        if (unlocked == null)
        {
            unlocked = doc.CreateElement("unlocked_blueprints");
            _element.AppendChild(unlocked);
        }
        var bytes = unlocked["bytes"];
        if (bytes == null)
        {
            bytes = doc.CreateElement("bytes");
            unlocked.AppendChild(bytes);
        }
        return bytes;
    }

    public string Kind =>
        IsPlayer ? "Player" :
        IsNeutral ? "Neutral" :
        IsAi ? "Enemy (AI)" : "Human";

    public override string ToString()
    {
        var tag = IsPlayer ? "★ " : "";
        var dead = IsDestroyed ? " [destroyed]" : "";
        return $"{tag}Team {Id} — {Kind}{dead}";
    }
}

/// <summary>A located occurrence of the searched-for currency value.</summary>
public sealed class CurrencyMatch
{
    internal XmlAttribute? Attr { get; init; }
    internal XmlElement? Element { get; init; }

    public required long Value { get; init; }
    public required string Description { get; init; }
    public int Score { get; init; }

    /// <summary>True when this looks like the player's budget (near the human team / money-named field).</summary>
    public bool LikelyBudget => Score >= 25;

    public void Apply(long newValue)
    {
        if (Attr != null) Attr.Value = newValue.ToString(CultureInfo.InvariantCulture);
        else if (Element != null) Element.InnerText = newValue.ToString(CultureInfo.InvariantCulture);
    }
}
