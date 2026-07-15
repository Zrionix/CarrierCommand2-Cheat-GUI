namespace CC2CheatGUI.Core.Ram;

/// <summary>Scans a process's committed memory for AOB signatures and exact values.</summary>
public sealed class MemoryScanner
{
    private readonly ProcessMemory _mem;
    private const int ChunkSize = 0x100000; // 1 MB read chunks

    public MemoryScanner(ProcessMemory mem) => _mem = mem;

    /// <summary>All absolute addresses where <paramref name="sig"/> matches within the module image.</summary>
    public List<IntPtr> ScanModule(Signature sig, int limit = 256)
    {
        var results = new List<IntPtr>();
        var seen = new HashSet<long>();
        int overlap = sig.Length - 1;
        var buffer = new byte[ChunkSize + overlap];

        foreach (var region in _mem.EnumerateRegions(moduleOnly: true))
        {
            long baseAddr = region.Base.ToInt64();
            long size = region.Size;
            long pos = 0;
            while (pos < size)
            {
                int want = (int)Math.Min(ChunkSize + overlap, size - pos);
                int got = _mem.TryReadBytes(new IntPtr(baseAddr + pos), buffer, want);
                if (got <= 0) break;

                var span = buffer.AsSpan(0, got);
                int start = 0;
                while (true)
                {
                    int idx = sig.IndexOf(span, start);
                    if (idx < 0) break;
                    long abs = baseAddr + pos + idx;
                    if (seen.Add(abs))
                    {
                        results.Add(new IntPtr(abs));
                        if (results.Count >= limit) return results;
                    }
                    start = idx + 1;
                }

                if (got < want) break;
                pos += ChunkSize; // step by chunk; the +overlap tail covers cross-boundary matches
            }
        }
        return results;
    }

    /// <summary>All addresses holding the exact 4-byte int <paramref name="value"/> in writable private memory.</summary>
    public List<IntPtr> ScanInt32(int value, bool writableOnly = true, int limit = 100000)
    {
        var needle = BitConverter.GetBytes(value);
        var results = new List<IntPtr>();
        var seen = new HashSet<long>();
        var buffer = new byte[ChunkSize + 4];

        foreach (var region in _mem.EnumerateRegions(moduleOnly: false))
        {
            const uint PAGE_READWRITE = 0x04;
            const uint PAGE_WRITECOPY = 0x08;
            const uint PAGE_EXECUTE_READWRITE = 0x40;
            const uint PAGE_EXECUTE_WRITECOPY = 0x80;
            if (writableOnly)
            {
                bool w = region.Protect is PAGE_READWRITE or PAGE_WRITECOPY or PAGE_EXECUTE_READWRITE or PAGE_EXECUTE_WRITECOPY;
                if (!w) continue;
            }

            long baseAddr = region.Base.ToInt64();
            long size = region.Size;
            long pos = 0;
            while (pos < size)
            {
                int want = (int)Math.Min(ChunkSize + 4, size - pos);
                int got = _mem.TryReadBytes(new IntPtr(baseAddr + pos), buffer, want);
                if (got <= 0) break;

                for (int i = 0; i + 4 <= got; i++)
                {
                    if (buffer[i] == needle[0] && buffer[i + 1] == needle[1] &&
                        buffer[i + 2] == needle[2] && buffer[i + 3] == needle[3])
                    {
                        long abs = baseAddr + pos + i;
                        if (seen.Add(abs))
                        {
                            results.Add(new IntPtr(abs));
                            if (results.Count >= limit) return results;
                        }
                    }
                }
                if (got < want) break;
                pos += ChunkSize;
            }
        }
        return results;
    }

    /// <summary>Filter a prior result set down to addresses that now hold <paramref name="value"/>.</summary>
    public List<IntPtr> Refine(IEnumerable<IntPtr> candidates, int value)
    {
        var kept = new List<IntPtr>();
        foreach (var addr in candidates)
        {
            try { if (_mem.Read<int>(addr) == value) kept.Add(addr); }
            catch { /* region went away */ }
        }
        return kept;
    }
}
