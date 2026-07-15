using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CC2CheatGUI.Core.Ram;

/// <summary>
/// Attaches to a running process and reads/writes its memory via kernel32 P/Invoke.
/// Trainer-scoped access mask only (no PROCESS_ALL_ACCESS). x64 host required to touch a
/// 64-bit target's modules.
/// </summary>
public sealed class ProcessMemory : IDisposable
{
    // VM_OPERATION | VM_READ | VM_WRITE | QUERY_INFORMATION
    private const uint Access = 0x0008 | 0x0010 | 0x0020 | 0x0400;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, [Out] byte[] buffer, nuint size, out nuint read);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, [In] byte[] buffer, nuint size, out nuint written);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint VirtualQueryEx(IntPtr h, IntPtr addr, out MEMORY_BASIC_INFORMATION64 mbi, nuint len);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr h);

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION64
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public uint __alignment1;
        public IntPtr RegionSize;
        public uint State;    // MEM_COMMIT = 0x1000
        public uint Protect;  // 0x04 RW, 0x40 ERW, 0x100 GUARD, 0x01 NOACCESS
        public uint Type;      // 0x1000000 IMAGE, 0x20000 PRIVATE
        public uint __alignment2;
    }

    private IntPtr _handle;

    public Process Process { get; }
    public IntPtr ModuleBase { get; }
    public int ModuleSize { get; }
    public bool IsAttached => _handle != IntPtr.Zero;

    private ProcessMemory(Process process, IntPtr handle, IntPtr moduleBase, int moduleSize)
    {
        Process = process;
        _handle = handle;
        ModuleBase = moduleBase;
        ModuleSize = moduleSize;
    }

    /// <summary>Attach to the first process with the given name (without ".exe").</summary>
    public static ProcessMemory Attach(string processName)
    {
        if (!Environment.Is64BitProcess)
            throw new InvalidOperationException("The trainer must run as a 64-bit process to access Carrier Command 2.");

        var proc = Process.GetProcessesByName(processName).FirstOrDefault()
            ?? throw new InvalidOperationException($"'{processName}' is not running. Start Carrier Command 2 first.");

        var module = proc.MainModule
            ?? throw new InvalidOperationException("Could not read the game's main module.");

        var handle = OpenProcess(Access, false, proc.Id);
        if (handle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            string hint = err == 5
                ? "Access denied — is the game running as administrator? Run this tool elevated too."
                : $"OpenProcess failed (Win32 {err}).";
            throw new InvalidOperationException(hint);
        }

        return new ProcessMemory(proc, handle, module.BaseAddress, module.ModuleMemorySize);
    }

    public byte[] ReadBytes(IntPtr addr, int count)
    {
        var buf = new byte[count];
        if (!ReadProcessMemory(_handle, addr, buf, (nuint)count, out var read) || (int)read != count)
            throw new IOException($"RPM failed @ 0x{addr.ToInt64():X} ({Marshal.GetLastWin32Error()})");
        return buf;
    }

    /// <summary>Read up to <paramref name="count"/> bytes; returns however many succeeded (0 on failure).</summary>
    public int TryReadBytes(IntPtr addr, byte[] buffer, int count)
    {
        if (ReadProcessMemory(_handle, addr, buffer, (nuint)count, out var read))
            return (int)read;
        return 0;
    }

    public void WriteBytes(IntPtr addr, byte[] bytes)
    {
        if (!WriteProcessMemory(_handle, addr, bytes, (nuint)bytes.Length, out var written) || (int)written != bytes.Length)
            throw new IOException($"WPM failed @ 0x{addr.ToInt64():X} ({Marshal.GetLastWin32Error()})");
    }

    public T Read<T>(IntPtr addr) where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        var buf = ReadBytes(addr, size);
        return MemoryMarshal.Read<T>(buf);
    }

    public void Write<T>(IntPtr addr, T value) where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        var buf = new byte[size];
        MemoryMarshal.Write(buf, in value);
        WriteBytes(addr, buf);
    }

    /// <summary>Committed, readable, non-guard regions. If <paramref name="moduleOnly"/>, only the exe image window.</summary>
    public IEnumerable<(IntPtr Base, long Size, uint Protect, uint Type)> EnumerateRegions(bool moduleOnly)
    {
        long start = moduleOnly ? ModuleBase.ToInt64() : 0x10000;
        long end = moduleOnly ? ModuleBase.ToInt64() + ModuleSize : 0x7FFFFFFFFFFF;
        IntPtr addr = new(start);
        nuint mbiSize = (nuint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION64>();

        while (addr.ToInt64() < end)
        {
            if (VirtualQueryEx(_handle, addr, out var mbi, mbiSize) == 0) break;
            long regionSize = mbi.RegionSize.ToInt64();
            if (regionSize <= 0) break;

            const uint MEM_COMMIT = 0x1000;
            const uint PAGE_GUARD = 0x100;
            const uint PAGE_NOACCESS = 0x01;
            bool readable = mbi.State == MEM_COMMIT
                            && (mbi.Protect & PAGE_GUARD) == 0
                            && (mbi.Protect & PAGE_NOACCESS) == 0
                            && mbi.Protect != 0;
            if (readable)
                yield return (mbi.BaseAddress, regionSize, mbi.Protect, mbi.Type);

            long next = mbi.BaseAddress.ToInt64() + regionSize;
            if (next <= addr.ToInt64()) break;
            addr = new IntPtr(next);
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
