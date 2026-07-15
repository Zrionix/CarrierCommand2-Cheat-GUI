using System.Globalization;
using System.Xml;

namespace CC2CheatGUI.Core;

/// <summary>
/// A single live vehicle's state. CC2 stores each vehicle's data as an escaped XML document inside
/// the <c>state</c> attribute of its <c>&lt;v&gt;</c> node under <c>&lt;vehicle_states&gt;</c>. That
/// inner document holds the vehicle's <c>hitpoints</c> / <c>internal_fuel_remaining</c> (on
/// <c>&lt;data&gt;</c>) and, for cargo-carrying vehicles, an <c>&lt;item_quantities&gt;</c> hold.
///
/// Because the hold inventory AND the hitpoints/fuel live in the *same* escaped blob, we parse it
/// exactly once here and share this object between the inventory view and the fleet view, so their
/// edits never clobber one another. Per-weapon ammo lives in separate escaped <c>&lt;a&gt;</c>
/// attachment states, wrapped by <see cref="AttachmentState"/> and flushed together.
/// </summary>
public sealed class VehicleState
{
    private readonly XmlAttribute _stateAttr;
    private readonly Cc2Document _inner;
    private readonly XmlElement _data;                 // inner <data ...>
    private readonly List<XmlElement> _slots;          // positional <q value="N"/> children (may be empty)
    private bool _dirty;

    public string Id { get; }
    public string TeamId { get; }
    public string DefinitionIndex { get; }
    public IReadOnlyList<AttachmentState> Attachments { get; }

    private VehicleState(XmlElement node, XmlAttribute stateAttr, Cc2Document inner, XmlElement data,
        List<XmlElement> slots, List<AttachmentState> attachments)
    {
        _stateAttr = stateAttr;
        _inner = inner;
        _data = data;
        _slots = slots;
        Attachments = attachments;
        Id = node.GetAttribute("id");
        TeamId = node.GetAttribute("team_id");
        DefinitionIndex = node.GetAttribute("definition_index");
    }

    public bool HasInventory => _slots.Count > 0;
    public int SlotCount => _slots.Count;

    public long? Hitpoints
    {
        get => TryLong(_data.GetAttribute("hitpoints"));
        set { if (value is long v) { _data.SetAttribute("hitpoints", v.ToString(CultureInfo.InvariantCulture)); _dirty = true; } }
    }

    public bool HasHitpoints => _data.HasAttribute("hitpoints");

    /// <summary>Remaining fuel (float, scientific notation in the file). Null if the vehicle has none.</summary>
    public double? Fuel
    {
        get => TryDouble(_data.GetAttribute("internal_fuel_remaining"));
        set { if (value is double v) { _data.SetAttribute("internal_fuel_remaining", Sci(v)); _dirty = true; } }
    }

    public bool HasFuel => _data.HasAttribute("internal_fuel_remaining");

    public bool IsDestroyed =>
        _data.GetAttribute("is_destroyed").Trim().ToLowerInvariant() is "true" or "1";

    public void SetDestroyed(bool destroyed)
    {
        _data.SetAttribute("is_destroyed", destroyed ? "true" : "false");
        _data.SetAttribute("is_trigger_destroy", destroyed ? "true" : "false");
        _dirty = true;
    }

    // ----- hold inventory (positional) -----

    public IReadOnlyList<(int ItemId, long Quantity)> Items
    {
        get
        {
            var list = new List<(int, long)>(_slots.Count);
            for (int i = 0; i < _slots.Count; i++)
                list.Add((i, TryLong(_slots[i].GetAttribute("value")) ?? 0));
            return list;
        }
    }

    public bool SetItem(int itemId, long quantity)
    {
        if (itemId < 0 || itemId >= _slots.Count) return false;
        _slots[itemId].SetAttribute("value", quantity.ToString(CultureInfo.InvariantCulture));
        _dirty = true;
        return true;
    }

    // ----- flush -----

    public void Flush()
    {
        foreach (var a in Attachments) a.Flush();
        if (!_dirty) return;
        _stateAttr.Value = _inner.ToXmlString();
        _dirty = false;
    }

    /// <summary>Parse a <c>&lt;v&gt;</c> state node, or return null if its state isn't parseable.</summary>
    public static VehicleState? TryCreate(XmlElement node)
    {
        var attr = node.GetAttributeNode("state");
        if (attr == null || string.IsNullOrWhiteSpace(attr.Value)) return null;

        Cc2Document inner;
        try { inner = Cc2Document.Parse(attr.Value); }
        catch (XmlException) { return null; }

        var data = FirstDescendant(inner.Root, "data");
        if (data == null) return null;

        var slots = new List<XmlElement>();
        var iq = FirstDescendant(inner.Root, "item_quantities");
        if (iq != null)
            foreach (XmlNode c in iq.ChildNodes)
                if (c is XmlElement el) slots.Add(el);

        // Attachments (separate escaped states holding ammo) live under <attachments> in the OUTER node.
        var attachments = new List<AttachmentState>();
        var attContainer = node["attachments"];
        if (attContainer != null)
            foreach (XmlNode c in attContainer.ChildNodes)
                if (c is XmlElement a && a.Name == "a")
                {
                    var att = AttachmentState.TryCreate(a);
                    if (att != null) attachments.Add(att);
                }

        return new VehicleState(node, attr, inner, data, slots, attachments);
    }

    internal static XmlElement? FirstDescendant(XmlNode? root, string name)
    {
        if (root == null) return null;
        foreach (XmlNode node in root.ChildNodes)
        {
            if (node is XmlElement el)
            {
                if (el.Name == name) return el;
                var nested = FirstDescendant(el, name);
                if (nested != null) return nested;
            }
        }
        return null;
    }

    internal static long? TryLong(string s) =>
        long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    internal static double? TryDouble(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>Format like the game: mantissa e exponent, e.g. 9.99999000e+04.</summary>
    internal static string Sci(double v) => v.ToString("0.00000000e+00", CultureInfo.InvariantCulture);
}

/// <summary>A weapon/attachment's escaped state, exposing its live ammo count.</summary>
public sealed class AttachmentState
{
    private readonly XmlAttribute _stateAttr;
    private readonly Cc2Document _inner;
    private readonly XmlElement _data;
    private bool _dirty;

    public string Index { get; }

    private AttachmentState(XmlElement node, XmlAttribute stateAttr, Cc2Document inner, XmlElement data)
    {
        _stateAttr = stateAttr;
        _inner = inner;
        _data = data;
        Index = node.GetAttribute("attachment_index");
    }

    public bool HasAmmo => _data.HasAttribute("ammo");

    public long? Ammo
    {
        get => VehicleState.TryLong(_data.GetAttribute("ammo"));
        set { if (value is long v) { _data.SetAttribute("ammo", v.ToString(CultureInfo.InvariantCulture)); _dirty = true; } }
    }

    public void Flush()
    {
        if (!_dirty) return;
        _stateAttr.Value = _inner.ToXmlString();
        _dirty = false;
    }

    public static AttachmentState? TryCreate(XmlElement node)
    {
        var attr = node.GetAttributeNode("state");
        if (attr == null || string.IsNullOrWhiteSpace(attr.Value)) return null;
        Cc2Document inner;
        try { inner = Cc2Document.Parse(attr.Value); }
        catch (XmlException) { return null; }
        var data = VehicleState.FirstDescendant(inner.Root, "data");
        if (data == null) return null;
        return new AttachmentState(node, attr, inner, data);
    }
}
