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

    /// <summary>
    /// Find every copy of a known positional int32 row in writable memory. Anchors on the row values
    /// at <paramref name="anchorSlots"/> (found in a single pass), then keeps each candidate base whose
    /// window matches at least <paramref name="minMatch"/> of the row's values — tolerating drift in
    /// consumed slots. The game keeps several synchronized copies of a vehicle hold; returning them all
    /// lets the caller write to every one so the change actually takes effect.
    /// </summary>
    public List<IntPtr> FindRowCopies(int[] row, int[] anchorSlots, int minMatch, int limit = 512)
    {
        var found = new List<IntPtr>();
        if (row.Length == 0 || anchorSlots.Length == 0) return found;

        // value -> the anchor slot indices that hold it (usually one).
        var valToSlots = new Dictionary<int, List<int>>();
        foreach (var sid in anchorSlots)
        {
            if (sid < 0 || sid >= row.Length) continue;
            if (!valToSlots.TryGetValue(row[sid], out var list)) valToSlots[row[sid]] = list = new List<int>();
            list.Add(sid);
        }

        var seen = new HashSet<long>();
        int rowBytes = row.Length * 4;
        var rowBuf = new byte[rowBytes];
        var buffer = new byte[ChunkSize + 4];

        foreach (var region in _mem.EnumerateRegions(moduleOnly: false))
        {
            const uint PAGE_READWRITE = 0x04, PAGE_WRITECOPY = 0x08,
                       PAGE_EXECUTE_READWRITE = 0x40, PAGE_EXECUTE_WRITECOPY = 0x80;
            if (region.Protect is not (PAGE_READWRITE or PAGE_WRITECOPY or PAGE_EXECUTE_READWRITE or PAGE_EXECUTE_WRITECOPY))
                continue;

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
                    int val = BitConverter.ToInt32(buffer, i);
                    if (!valToSlots.TryGetValue(val, out var sids)) continue;
                    foreach (var sid in sids)
                    {
                        long cand = baseAddr + pos + i - (long)sid * 4;
                        if (!seen.Add(cand)) continue;

                        // Verify from the already-read buffer when the whole row fits inside it
                        // (avoids a syscall per candidate — the common case). Fall back to a read.
                        long rel = cand - (baseAddr + pos);
                        int m = 0;
                        if (rel >= 0 && rel + rowBytes <= got)
                        {
                            int off = (int)rel;
                            for (int k = 0; k < row.Length; k++)
                                if (BitConverter.ToInt32(buffer, off + k * 4) == row[k]) m++;
                        }
                        else
                        {
                            if (_mem.TryReadBytes(new IntPtr(cand), rowBuf, rowBytes) != rowBytes) continue;
                            for (int k = 0; k < row.Length; k++)
                                if (BitConverter.ToInt32(rowBuf, k * 4) == row[k]) m++;
                        }
                        if (m >= minMatch)
                        {
                            found.Add(new IntPtr(cand));
                            if (found.Count >= limit) return found;
                        }
                    }
                }
                if (got < want) break;
                pos += ChunkSize;
            }
        }
        return found;
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
