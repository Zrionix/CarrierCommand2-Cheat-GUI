using System.Globalization;
using System.Xml;

namespace CC2CheatGUI.Core;

/// <summary>A single located/edited Carrier Command 2 save.xml.</summary>
public sealed class SaveFile
{
    private readonly XmlDocument _doc;

    public string Path { get; }
    public IReadOnlyList<InventoryContainer> Containers { get; private set; } = Array.Empty<InventoryContainer>();

    /// <summary>The non-AI team id, when one could be identified ("" otherwise).</summary>
    public string PlayerTeamId { get; private set; } = "";

    private SaveFile(string path, XmlDocument doc)
    {
        Path = path;
        _doc = doc;
    }

    public static SaveFile Load(string path)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.Load(path);
        var save = new SaveFile(path, doc);
        save.Discover();
        return save;
    }

    // ---------------------------------------------------------------------
    // Discovery
    // ---------------------------------------------------------------------

    private void Discover()
    {
        IdentifyPlayerTeam();

        var containers = new List<InventoryContainer>();
        containers.AddRange(DiscoverVehicleHolds());
        containers.AddRange(DiscoverIslandStock());
        Containers = containers;
    }

    private void IdentifyPlayerTeam()
    {
        var teams = _doc.SelectNodes("//*[@is_ai_controlled]");
        if (teams == null) return;

        string firstHuman = "";
        foreach (XmlNode node in teams)
        {
            if (node is not XmlElement el) continue;
            var ai = el.GetAttribute("is_ai_controlled").Trim().ToLowerInvariant();
            bool isAi = ai is "true" or "1";
            if (!isAi)
            {
                var id = el.GetAttribute("id");
                if (id.Length == 0) id = el.GetAttribute("team_id");
                if (firstHuman.Length == 0) firstHuman = id;
            }
        }
        PlayerTeamId = firstHuman;
    }

    private List<InventoryContainer> DiscoverVehicleHolds()
    {
        var result = new List<InventoryContainer>();

        // Map vehicle id -> (team_id, definition_index) for labelling.
        var vehicleInfo = new Dictionary<string, (string Team, string Def)>();
        var vehicleNodes = _doc.SelectNodes("//*[@definition_index]");
        if (vehicleNodes != null)
        {
            foreach (XmlNode node in vehicleNodes)
            {
                if (node is not XmlElement el) continue;
                var id = el.GetAttribute("id");
                if (id.Length == 0) continue;
                if (!vehicleInfo.ContainsKey(id))
                    vehicleInfo[id] = (el.GetAttribute("team_id"), el.GetAttribute("definition_index"));
            }
        }

        var stateNodes = _doc.SelectNodes("//*[@state]");
        if (stateNodes == null) return result;

        int anon = 0;
        foreach (XmlNode node in stateNodes)
        {
            if (node is not XmlElement stateNode) continue;
            var id = stateNode.GetAttribute("id");

            string label;
            bool yours = false;
            if (id.Length > 0 && vehicleInfo.TryGetValue(id, out var info))
            {
                yours = PlayerTeamId.Length > 0 && info.Team == PlayerTeamId;
                string type = DescribeVehicle(info.Def);
                label = $"{type} — team {info.Team} (vehicle {id})";
            }
            else
            {
                label = id.Length > 0 ? $"Vehicle hold (id {id})" : $"Vehicle hold #{++anon}";
            }
            if (yours) label = "★ " + label + "  [YOURS]";

            var hold = VehicleHoldContainer.TryCreate(stateNode, label);
            if (hold == null) continue;
            result.Add(hold);
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

    private static string DescribeVehicle(string definitionIndex)
    {
        return definitionIndex switch
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
            "" => "Vehicle",
            _ => $"Vehicle (def {definitionIndex})",
        };
    }

    // ---------------------------------------------------------------------
    // Currency
    // ---------------------------------------------------------------------

    /// <summary>
    /// Find every place in the save that holds the exact integer <paramref name="value"/> (the
    /// amount the player should read from their in-game money display). Matches near the player's
    /// team data are flagged as the likely budget field.
    /// </summary>
    public List<CurrencyMatch> FindValue(long value)
    {
        var matches = new List<CurrencyMatch>();
        WalkForValue(_doc.DocumentElement, value, matches);

        // Likely-budget matches (near player team / money-ish names) first.
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
        foreach (var container in Containers)
            container.Flush();

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
