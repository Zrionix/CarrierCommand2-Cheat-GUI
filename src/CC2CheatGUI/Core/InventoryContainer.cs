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
/// A carrier or deployed-unit hold. The inventory lives inside the vehicle's escaped
/// <c>state</c> blob (a positional <c>&lt;item_quantities&gt;</c> list, child index == item ID),
/// which is parsed once by <see cref="VehicleState"/> and shared with the fleet view. This
/// container is a thin inventory-facing adapter over that shared state.
/// </summary>
public sealed class VehicleHoldContainer : InventoryContainer
{
    private readonly VehicleState _state;

    private VehicleHoldContainer(string label, VehicleState state)
    {
        Label = label;
        Kind = ContainerKind.VehicleHold;
        _state = state;
    }

    public VehicleState State => _state;

    public override IReadOnlyList<(int ItemId, long Quantity)> Entries => _state.Items;

    public override bool SetQuantity(int itemId, long quantity) => _state.SetItem(itemId, quantity);

    // The owning VehicleState is flushed once by SaveFile.Save(); nothing to do here.
    public override void Flush() { }

    /// <summary>Wrap a parsed vehicle state that carries a hold. Returns null if it has no inventory.</summary>
    public static VehicleHoldContainer? TryCreate(VehicleState state, string label) =>
        state.HasInventory ? new VehicleHoldContainer(label, state) : null;
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
