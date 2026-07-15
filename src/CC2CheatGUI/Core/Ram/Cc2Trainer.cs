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
