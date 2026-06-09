using System.Text;
using System.Xml;

namespace CC2CheatGUI.Core;

public enum ContainerKind
{
    VehicleHold,   // carrier or deployed unit — positional item_quantities inside the escaped "state" attribute
    IslandStock,   // island / warehouse — sparse <q i="" q=""/> list
}

/// <summary>One editable inventory location inside a save.</summary>
public abstract class InventoryContainer
{
    public string Label { get; protected set; } = "";
    public ContainerKind Kind { get; protected set; }

    /// <summary>Current (itemId, quantity) pairs, ordered for display.</summary>
    public abstract IReadOnlyList<(int ItemId, long Quantity)> Entries { get; }

    /// <summary>Set (or add) the quantity for an item ID. Returns false if it could not be applied.</summary>
    public abstract bool SetQuantity(int itemId, long quantity);

    /// <summary>Write any pending in-memory edits back into the owning XML document.</summary>
    public abstract void Flush();

    public override string ToString() => Label;
}

/// <summary>
/// A carrier or deployed-unit hold. The inventory lives as a nested, XML-escaped document held
/// in the <c>state</c> attribute of the vehicle's state node:
/// <c>state="&lt;data&gt;...&lt;inventory&gt;&lt;item_quantities&gt;&lt;... value="N"/&gt;...&lt;/item_quantities&gt;..."</c>.
/// The children of <c>item_quantities</c> are positional: child index == item ID.
/// </summary>
public sealed class VehicleHoldContainer : InventoryContainer
{
    private readonly XmlAttribute _stateAttr;     // the "state" attribute on the main document
    private readonly XmlDocument _innerDoc;       // the un-nested inventory document
    private readonly List<XmlElement> _slots;     // positional children of item_quantities
    private bool _dirty;

    private VehicleHoldContainer(
        string label, XmlAttribute stateAttr, XmlDocument innerDoc, List<XmlElement> slots)
    {
        Label = label;
        Kind = ContainerKind.VehicleHold;
        _stateAttr = stateAttr;
        _innerDoc = innerDoc;
        _slots = slots;
    }

    public override IReadOnlyList<(int ItemId, long Quantity)> Entries
    {
        get
        {
            var list = new List<(int, long)>(_slots.Count);
            for (int i = 0; i < _slots.Count; i++)
                list.Add((i, ParseLong(_slots[i].GetAttribute("value"))));
            return list;
        }
    }

    public override bool SetQuantity(int itemId, long quantity)
    {
        // Holds are positional with a fixed slot count; only existing slots can be set.
        if (itemId < 0 || itemId >= _slots.Count) return false;
        _slots[itemId].SetAttribute("value", quantity.ToString());
        _dirty = true;
        return true;
    }

    public override void Flush()
    {
        if (!_dirty) return;
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
            ConformanceLevel = ConformanceLevel.Fragment,
        };
        using (var writer = XmlWriter.Create(sb, settings))
        {
            // Write only the document element so no <?xml ...?> declaration is reintroduced.
            _innerDoc.DocumentElement!.WriteTo(writer);
        }
        // Assigning to the attribute value re-escapes it automatically when the main doc is saved.
        _stateAttr.Value = sb.ToString();
        _dirty = false;
    }

    /// <summary>
    /// Try to build a hold container from a state node. Returns null when the node's "state"
    /// attribute does not contain a parseable data/inventory/item_quantities block.
    /// </summary>
    public static VehicleHoldContainer? TryCreate(XmlElement stateNode, string label)
    {
        var attr = stateNode.GetAttributeNode("state");
        if (attr == null || string.IsNullOrWhiteSpace(attr.Value)) return null;

        XmlDocument inner = new() { PreserveWhitespace = true };
        try
        {
            inner.LoadXml(attr.Value);
        }
        catch (XmlException)
        {
            return null;
        }

        XmlElement? itemQuantities = FindFirstDescendant(inner.DocumentElement, "item_quantities");
        if (itemQuantities == null) return null;

        var slots = new List<XmlElement>();
        foreach (XmlNode child in itemQuantities.ChildNodes)
            if (child is XmlElement el)
                slots.Add(el);

        if (slots.Count == 0) return null;
        return new VehicleHoldContainer(label, attr, inner, slots);
    }

    private static XmlElement? FindFirstDescendant(XmlNode? root, string name)
    {
        if (root == null) return null;
        foreach (XmlNode node in root.ChildNodes)
        {
            if (node is XmlElement el)
            {
                if (el.Name == name) return el;
                var nested = FindFirstDescendant(el, name);
                if (nested != null) return nested;
            }
        }
        return null;
    }

    private static long ParseLong(string s) =>
        long.TryParse(s, out var v) ? v : 0;
}

/// <summary>
/// An island / warehouse stockpile. Inventory is a sparse list of self-closing
/// <c>&lt;q i="ITEM_ID" q="QUANTITY"/&gt;</c> elements directly in the main document.
/// </summary>
public sealed class IslandStockContainer : InventoryContainer
{
    private readonly XmlElement _parent;                  // element that holds the <q> children
    private readonly Dictionary<int, XmlElement> _byItem; // itemId -> <q> element

    private IslandStockContainer(string label, XmlElement parent, Dictionary<int, XmlElement> byItem)
    {
        Label = label;
        Kind = ContainerKind.IslandStock;
        _parent = parent;
        _byItem = byItem;
    }

    public override IReadOnlyList<(int ItemId, long Quantity)> Entries
    {
        get
        {
            var list = new List<(int, long)>(_byItem.Count);
            foreach (var kv in _byItem)
                list.Add((kv.Key, ParseLong(kv.Value.GetAttribute("q"))));
            list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return list;
        }
    }

    public override bool SetQuantity(int itemId, long quantity)
    {
        if (_byItem.TryGetValue(itemId, out var el))
        {
            el.SetAttribute("q", quantity.ToString());
            return true;
        }

        // Add a new <q i="" q=""/> entry, matching the element name already in use.
        var doc = _parent.OwnerDocument!;
        var created = doc.CreateElement("q");
        created.SetAttribute("i", itemId.ToString());
        created.SetAttribute("q", quantity.ToString());
        _parent.AppendChild(created);
        _byItem[itemId] = created;
        return true;
    }

    public override void Flush()
    {
        // Edits are applied directly to the main document's nodes; nothing to do.
    }

    /// <summary>Build a stock container from a parent element that owns &lt;q i= q=&gt; children.</summary>
    public static IslandStockContainer Create(XmlElement parent, IEnumerable<XmlElement> qElements, string label)
    {
        var byItem = new Dictionary<int, XmlElement>();
        foreach (var q in qElements)
            if (int.TryParse(q.GetAttribute("i"), out var id))
                byItem[id] = q; // last wins on duplicate ids
        return new IslandStockContainer(label, parent, byItem);
    }

    private static long ParseLong(string s) =>
        long.TryParse(s, out var v) ? v : 0;
}
