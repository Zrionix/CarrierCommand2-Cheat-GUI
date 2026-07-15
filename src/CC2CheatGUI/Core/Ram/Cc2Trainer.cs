using System.Globalization;

namespace CC2CheatGUI.Core.Ram;

/// <summary>One live cheat: a set of AOB signatures whose matched instructions get NOP-ed.</summary>
public sealed class TrainerCheat
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string[] Signatures { get; init; }
    public bool AffectsEnemies { get; init; }
    public bool Experimental { get; init; }
    public string Notes { get; init; } = "";

    // resolved at attach:
    internal List<(IntPtr Addr, byte[] Original)> Sites { get; } = new();
    public int FoundCount => Sites.Count;
    public bool Resolved => Sites.Count > 0;
    public bool Enabled { get; internal set; }
}

/// <summary>
/// Facade the UI talks to for live ("trainer") cheats. Attaches to the running game, resolves each
/// cheat's byte signatures against the current build (never hardcoded offsets), applies NOP patches,
/// and restores the original bytes on disable/detach. Also finds and writes the credit value.
/// </summary>
public sealed class Cc2Trainer : IDisposable
{
    private ProcessMemory? _mem;
    private MemoryScanner? _scanner;
    private System.Threading.Timer? _freezeTimer;

    public bool Attached => _mem?.IsAttached == true;
    public string StatusText { get; private set; } = "Not attached.";
    public IReadOnlyList<TrainerCheat> Cheats { get; } = DefaultCheats();

    // credit state
    public List<IntPtr> CreditCandidates { get; } = new();
    public int? FrozenCredit { get; private set; }

    public string ModuleInfo => _mem == null ? "" :
        $"carrier_command.exe  base=0x{_mem.ModuleBase.ToInt64():X}  {_mem.ModuleSize / (1024 * 1024)} MB";

    public void Attach()
    {
        Detach();
        _mem = ProcessMemory.Attach("carrier_command");
        _scanner = new MemoryScanner(_mem);

        int resolved = 0;
        foreach (var cheat in Cheats)
        {
            cheat.Sites.Clear();
            cheat.Enabled = false;
            var seen = new HashSet<long>();
            foreach (var sigText in cheat.Signatures)
            {
                var sig = new Signature(sigText);
                foreach (var addr in _scanner.ScanModule(sig))
                {
                    if (!seen.Add(addr.ToInt64())) continue;
                    var original = _mem.ReadBytes(addr, sig.Length);
                    cheat.Sites.Add((addr, original));
                }
            }
            if (cheat.Resolved) resolved++;
        }
        StatusText = $"Attached (PID {_mem.Process.Id}). {resolved}/{Cheats.Count} cheats resolved.";
    }

    public void EnableCheat(TrainerCheat cheat)
    {
        if (_mem == null || !cheat.Resolved) return;
        foreach (var (addr, original) in cheat.Sites)
        {
            var nops = new byte[original.Length];
            Array.Fill(nops, (byte)0x90);
            _mem.WriteBytes(addr, nops);
        }
        cheat.Enabled = true;
    }

    public void DisableCheat(TrainerCheat cheat)
    {
        if (_mem == null) return;
        foreach (var (addr, original) in cheat.Sites)
            _mem.WriteBytes(addr, original);
        cheat.Enabled = false;
    }

    public void ToggleCheat(TrainerCheat cheat, bool on)
    {
        if (on) EnableCheat(cheat); else DisableCheat(cheat);
    }

    // ---- credit ----

    /// <summary>Scan for the player's current credit value; narrows candidates on repeated calls.</summary>
    public int FindCredit(int currentValue)
    {
        if (_scanner == null) return 0;
        if (CreditCandidates.Count == 0)
            CreditCandidates.AddRange(_scanner.ScanInt32(currentValue));
        else
        {
            var refined = _scanner.Refine(CreditCandidates, currentValue);
            CreditCandidates.Clear();
            CreditCandidates.AddRange(refined);
        }
        return CreditCandidates.Count;
    }

    public void ResetCreditSearch() => CreditCandidates.Clear();

    /// <summary>Read the live credit from the first resolved candidate (null if none / unreadable).</summary>
    public int? ReadCredit()
    {
        if (_mem == null || CreditCandidates.Count == 0) return null;
        try { return _mem.Read<int>(CreditCandidates[0]); } catch { return null; }
    }

    /// <summary>Write a new credit value to every candidate address.</summary>
    public int SetCredit(int value)
    {
        if (_mem == null) return 0;
        int n = 0;
        foreach (var addr in CreditCandidates)
        {
            try { _mem.Write(addr, value); n++; } catch { }
        }
        return n;
    }

    // ---- live carrier hold ----

    private readonly List<IntPtr> _holdCopies = new();
    private int[] _holdRow = Array.Empty<int>();

    /// <summary>Number of in-memory copies of the located hold (0 = not located).</summary>
    public int HoldCopyCount => _holdCopies.Count;
    /// <summary>Positional slot count of the located hold.</summary>
    public int HoldSlotCount => _holdRow.Length;

    /// <summary>Locate every copy of the player's carrier hold (drift-tolerant fingerprint).</summary>
    public int LocateHold(int[] saveRow) => LocateInventory(saveRow, consumableDrift: true);

    /// <summary>
    /// Locate every copy of a positional inventory row in the running game by fingerprinting the row
    /// taken from the loaded save. <paramref name="consumableDrift"/> = true for the carrier hold (whose
    /// ammo/fuel drift as you play, so it anchors on stable non-consumable slots and tolerates a looser
    /// match); false for island warehouses (dense, stable stock — anchor on the most distinctive values
    /// and require nearly all of them to match). Returns the number of copies found.
    /// </summary>
    public int LocateInventory(int[] saveRow, bool consumableDrift)
    {
        _holdCopies.Clear();
        _holdRow = Array.Empty<int>();
        if (_mem == null || _scanner == null || saveRow.Length == 0) return 0;
        int n = saveRow.Length;
        int nonZero = saveRow.Count(v => v != 0);

        int[] anchorSlots;
        int minMatch;
        if (consumableDrift)
        {
            // Carrier hold: distinctive values (fast) + largest stable non-consumable slots.
            var anchors = new List<int>();
            anchors.AddRange(Enumerable.Range(0, n).Where(i => saveRow[i] >= 50));
            anchors.AddRange(Enumerable.Range(0, n)
                .Where(i => saveRow[i] >= 4 && IsNonConsumable(i))
                .OrderByDescending(i => saveRow[i]).Take(6));
            anchorSlots = anchors.Distinct().ToArray();
            if (anchorSlots.Length == 0)
                anchorSlots = Enumerable.Range(0, n).Where(i => saveRow[i] > 0)
                    .OrderByDescending(i => saveRow[i]).Take(8).ToArray();
            // WRITE SAFETY: require nearly all non-zero slots to match. A loose match can hit unrelated
            // memory (a hold row is ~1/4 zeros), and writing big item values there corrupts the game.
            // The UI re-reads the fresh save before locating, so real copies match almost exactly and
            // this stays selective without missing them.
            minMatch = (n - nonZero) + Math.Max(1, (int)(nonZero * 0.80));
        }
        else
        {
            // Warehouse: anchor on the most distinctive stock values and require essentially every
            // non-zero slot to match, so a mostly-zero row can't false-match a zeroed memory region.
            anchorSlots = Enumerable.Range(0, n).Where(i => saveRow[i] != 0)
                .OrderByDescending(i => saveRow[i]).Take(12).ToArray();
            minMatch = (n - nonZero) + Math.Max(1, (int)(nonZero * 0.85));
        }

        _holdCopies.AddRange(_scanner.FindRowCopies(saveRow, anchorSlots, minMatch));
        _holdRow = (int[])saveRow.Clone();
        return _holdCopies.Count;
    }

    /// <summary>
    /// True when a positional row is distinctive enough to locate in memory without risking false
    /// matches against zeroed regions. Sparse warehouses (few small values) are not safely findable.
    /// </summary>
    public static bool IsRowFindable(int[] row)
    {
        int big = row.Count(v => v >= 100);
        int med = row.Count(v => v >= 30);
        int nonZero = row.Count(v => v != 0);
        return (big >= 1 && nonZero >= 4) || med >= 5;
    }

    private static bool IsNonConsumable(int itemId)
    {
        var c = ItemCatalog.CategoryOf(itemId);
        return c is ItemCatalog.CatVehicles or ItemCatalog.CatTurrets or ItemCatalog.CatComponents;
    }

    /// <summary>Read the current live quantities of the located hold (from the first copy).</summary>
    public int[]? ReadHold()
    {
        if (_mem == null || _holdCopies.Count == 0 || _holdRow.Length == 0) return null;
        try
        {
            var buf = _mem.ReadBytes(_holdCopies[0], _holdRow.Length * 4);
            var vals = new int[_holdRow.Length];
            for (int i = 0; i < vals.Length; i++) vals[i] = BitConverter.ToInt32(buf, i * 4);
            return vals;
        }
        catch { return null; }
    }

    /// <summary>Write one slot's quantity to every located copy. Returns the number of copies written.</summary>
    public int WriteHoldSlot(int slot, int value)
    {
        if (_mem == null || slot < 0 || slot >= _holdRow.Length) return 0;
        int n = 0;
        foreach (var b in _holdCopies)
        {
            try { _mem.Write(new IntPtr(b.ToInt64() + slot * 4L), value); n++; } catch { }
        }
        return n;
    }

    /// <summary>
    /// Write a full/partial quantity row to every located copy that STILL holds our inventory row.
    /// Each copy is re-verified against the located fingerprint immediately before writing, so a region
    /// that was freed/reallocated since LOCATE can never receive a stray (potentially crashing) write.
    /// </summary>
    public int WriteHold(int[] values)
    {
        if (_mem == null || _holdCopies.Count == 0 || _holdRow.Length == 0) return 0;
        int len = _holdRow.Length;
        int nonZero = _holdRow.Count(v => v != 0);
        int needNonZero = Math.Max(1, (int)(nonZero * 0.70));
        var buf = new byte[len * 4];

        int written = 0;
        foreach (var b in _holdCopies)
        {
            // Re-verify this copy still matches the located row before touching it.
            if (_mem.TryReadBytes(b, buf, buf.Length) != buf.Length) continue;
            int mnz = 0;
            for (int i = 0; i < len; i++)
                if (_holdRow[i] != 0 && BitConverter.ToInt32(buf, i * 4) == _holdRow[i]) mnz++;
            if (mnz < needNonZero) continue;   // not our inventory anymore — skip for safety

            bool ok = true;
            for (int i = 0; i < values.Length && i < len; i++)
            {
                try { _mem.Write(new IntPtr(b.ToInt64() + i * 4L), values[i]); }
                catch { ok = false; break; }
            }
            if (ok) written++;
        }

        // Track what we just wrote so a follow-up APPLY re-verifies against the new state, not the old.
        if (written > 0)
        {
            var updated = (int[])_holdRow.Clone();
            for (int i = 0; i < values.Length && i < len; i++) updated[i] = values[i];
            _holdRow = updated;
        }
        return written;
    }

    /// <summary>Set every slot of the located hold to <paramref name="value"/> across all copies.</summary>
    public int FillHold(int value)
    {
        if (_holdRow.Length == 0) return 0;
        var vals = new int[_holdRow.Length];
        Array.Fill(vals, value);
        return WriteHold(vals);
    }

    public void ResetHold() { _holdCopies.Clear(); _holdRow = Array.Empty<int>(); }

    // ---- player-only carrier protection (HP freeze) ----

    private readonly List<IntPtr> _carrierHp = new();
    private System.Threading.Timer? _protectTimer;
    private int _protectValue = 100000;

    /// <summary>Number of resolved carrier HP fields (0 = not located).</summary>
    public int CarrierHpCount => _carrierHp.Count;
    public bool Protecting => _protectTimer != null;

    /// <summary>
    /// Locate the player carrier's HP field(s) by anchoring on its distinctive exact fuel value (the
    /// carrier carries ~50k fuel vs ~1k for units, so it's near-unique in memory), then taking the HP
    /// value where it sits inside the same struct. Player-specific: only the carrier's own struct.
    /// </summary>
    public int LocateCarrierProtect(int fuelBits, int hp, int windowBytes = 1024)
    {
        _carrierHp.Clear();
        if (_mem == null || _scanner == null || hp <= 0) return 0;

        var fuelHits = _scanner.ScanInt32(fuelBits);
        if (fuelHits.Count == 0 || fuelHits.Count > 64) return 0;   // 0 = fuel drifted; many = not distinctive

        var seen = new HashSet<long>();
        var buf = new byte[windowBytes];
        foreach (var f in fuelHits)
        {
            long start = f.ToInt64() - windowBytes / 2;
            int got = _mem.TryReadBytes(new IntPtr(start), buf, buf.Length);
            if (got <= 0) continue;
            for (int i = 0; i + 4 <= got; i += 4)
                if (BitConverter.ToInt32(buf, i) == hp && seen.Add(start + i))
                    _carrierHp.Add(new IntPtr(start + i));
        }
        return _carrierHp.Count;
    }

    /// <summary>Start freezing the located carrier HP field(s) at <paramref name="value"/> (player-only).</summary>
    public void StartProtect(int value)
    {
        _protectValue = value;
        foreach (var a in _carrierHp) { try { _mem!.Write(a, value); } catch { } }
        _protectTimer ??= new System.Threading.Timer(_ =>
        {
            if (_mem == null) return;
            foreach (var a in _carrierHp) { try { _mem.Write(a, _protectValue); } catch { } }
        }, null, 0, 150);
    }

    public void StopProtect()
    {
        _protectTimer?.Dispose();
        _protectTimer = null;
    }

    public void ResetProtect() { StopProtect(); _carrierHp.Clear(); }

    /// <summary>Current values at the located carrier HP field(s) (for status/verification).</summary>
    public int[] ReadCarrierHpValues()
    {
        var vals = new List<int>();
        if (_mem != null)
            foreach (var a in _carrierHp) { try { vals.Add(_mem.Read<int>(a)); } catch { } }
        return vals.ToArray();
    }

    public void FreezeCredit(int value)
    {
        FrozenCredit = value;
        _freezeTimer ??= new System.Threading.Timer(_ =>
        {
            if (_mem == null || FrozenCredit is not int v) return;
            foreach (var addr in CreditCandidates)
            {
                try { _mem.Write(addr, v); } catch { }
            }
        }, null, 0, 200);
    }

    public void UnfreezeCredit()
    {
        FrozenCredit = null;
        _freezeTimer?.Dispose();
        _freezeTimer = null;
    }

    public void Detach()
    {
        UnfreezeCredit();
        if (_mem != null)
        {
            foreach (var cheat in Cheats)
                if (cheat.Enabled) { try { DisableCheat(cheat); } catch { } }
        }
        CreditCandidates.Clear();
        ResetHold();
        ResetProtect();
        _mem?.Dispose();
        _mem = null;
        _scanner = null;
        StatusText = "Not attached.";
    }

    public void Dispose() => Detach();

    private static List<TrainerCheat> DefaultCheats() => new()
    {
        new TrainerCheat
        {
            Id = "unlimited_ammo",
            Label = "Unlimited Ammo (all weapons)",
            // Every weapon consumes ammo with dec dword ptr [reg+0xD0]; the register varies by weapon.
            Signatures = new[] { "FF 8E D0 00 00 00", "FF 8F D0 00 00 00", "FF 8B D0 00 00 00", "FF 8D D0 00 00 00" },
            AffectsEnemies = true,
            Notes = "NOPs every ammo-decrement site. Also affects enemy units (shared code).",
        },
        // NOTE: a "God Mode" cheat that blindly NOPs the hull-damage store was intentionally removed.
        // That store is shared by every unit, so patching it makes ENEMY units invincible too — which
        // makes the game unwinnable and its on/off effect confusing. A correct, player-only version
        // requires locking onto the player carrier in memory (a code-cave team check, or a targeted
        // health freeze) — tracked alongside the live-inventory targeting work, not a blind NOP.
    };
}
