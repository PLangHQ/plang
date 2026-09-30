using System.Runtime.InteropServices;

namespace app.module.screen.code.wayland;

/// <summary>
/// What .NET has no managed door for: file descriptors passed over a Unix socket (Wayland sends
/// shared memory, the keyboard map and clipboard pipes that way), shared memory mapped in, pipes,
/// and libxkbcommon (the keyboard map and its modifier state). Linux only.
/// </summary>
internal static class Native
{
    private const string C = "libc.so.6";   // "libc" would look for libc.so, which only dev packages ship

    [StructLayout(LayoutKind.Sequential)]
    private struct IoVec { public IntPtr Base; public nuint Length; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsgHdr
    {
        public IntPtr Name; public uint NameLength; public IntPtr Iov; public nuint IovLength;
        public IntPtr Control; public nuint ControlLength; public int Flags;
    }

    [DllImport(C, SetLastError = true)] private static extern nint recvmsg(int fd, ref MsgHdr message, int flags);
    [DllImport(C, SetLastError = true)] private static extern nint sendmsg(int fd, ref MsgHdr message, int flags);
    [DllImport(C, SetLastError = true)] private static extern IntPtr mmap(IntPtr address, nuint length, int protection, int flags, int fd, nint offset);
    [DllImport(C, SetLastError = true)] private static extern int munmap(IntPtr address, nuint length);
    [DllImport(C, SetLastError = true)] internal static extern int close(int fd);
    [DllImport(C, SetLastError = true)] private static extern int memfd_create(string name, uint flags);
    [DllImport(C, SetLastError = true)] private static extern nint write(int fd, byte[] buffer, nuint count);
    [DllImport(C, SetLastError = true)] private static extern nint read(int fd, byte[] buffer, nuint count);
    [DllImport(C, SetLastError = true)] private static extern int pipe2(int[] fds, int flags);
    [DllImport(C)] private static extern void free(IntPtr pointer);

    private const int SolSocket = 1, ScmRights = 1, MsgCmsgCloexec = 0x40000000, MsgNoSignal = 0x4000;
    private const int ProtRead = 1, MapShared = 1, OCloexec = 0x80000;
    private const int EINTR = 4;

    /// <summary>Reads what the client sent into <paramref name="into"/>; file descriptors that came
    /// with it go into <paramref name="fds"/>. 0 when the client hung up.</summary>
    internal static int Receive(int socket, byte[] into, Queue<int> fds)
    {
        var data = Marshal.AllocHGlobal(into.Length);
        const int control = 16 + 4 * 28;   // room for 28 descriptors
        var cmsg = Marshal.AllocHGlobal(control);
        var iov = Marshal.AllocHGlobal(Marshal.SizeOf<IoVec>());
        try
        {
            Marshal.StructureToPtr(new IoVec { Base = data, Length = (nuint)into.Length }, iov, false);
            var header = new MsgHdr { Iov = iov, IovLength = 1, Control = cmsg, ControlLength = control };
            nint n;
            do n = recvmsg(socket, ref header, MsgCmsgCloexec);
            while (n < 0 && Marshal.GetLastWin32Error() == EINTR);
            if (n <= 0) return 0;
            Marshal.Copy(data, into, 0, (int)n);
            // control messages: [u64 length][i32 level][i32 type][data…], each padded to 8
            var at = 0;
            var used = (int)header.ControlLength;
            while (at + 16 <= used)
            {
                var length = (int)Marshal.ReadInt64(cmsg, at);
                if (length < 16) break;
                if (Marshal.ReadInt32(cmsg, at + 8) == SolSocket && Marshal.ReadInt32(cmsg, at + 12) == ScmRights)
                    for (var i = 16; i + 4 <= length; i += 4) fds.Enqueue(Marshal.ReadInt32(cmsg, at + i));
                at += (length + 7) & ~7;
            }
            return (int)n;
        }
        finally
        {
            Marshal.FreeHGlobal(data);
            Marshal.FreeHGlobal(cmsg);
            Marshal.FreeHGlobal(iov);
        }
    }

    /// <summary>Sends <paramref name="bytes"/>, with <paramref name="fds"/> alongside. False when the
    /// client is gone.</summary>
    internal static bool Send(int socket, byte[] bytes, int count, IReadOnlyList<int> fds)
    {
        var data = Marshal.AllocHGlobal(Math.Max(count, 1));
        var control = fds.Count == 0 ? 0 : 16 + ((4 * fds.Count + 7) & ~7);
        var cmsg = control == 0 ? IntPtr.Zero : Marshal.AllocHGlobal(control);
        var iov = Marshal.AllocHGlobal(Marshal.SizeOf<IoVec>());
        try
        {
            Marshal.Copy(bytes, 0, data, count);
            if (control > 0)
            {
                Marshal.WriteInt64(cmsg, 0, 16 + 4 * fds.Count);
                Marshal.WriteInt32(cmsg, 8, SolSocket);
                Marshal.WriteInt32(cmsg, 12, ScmRights);
                for (var i = 0; i < fds.Count; i++) Marshal.WriteInt32(cmsg, 16 + 4 * i, fds[i]);
            }
            var sent = 0;
            while (sent < count)
            {
                Marshal.StructureToPtr(new IoVec { Base = data + sent, Length = (nuint)(count - sent) }, iov, false);
                var header = new MsgHdr { Iov = iov, IovLength = 1, Control = sent == 0 ? cmsg : IntPtr.Zero, ControlLength = sent == 0 ? (nuint)control : 0 };
                var n = sendmsg(socket, ref header, MsgNoSignal);
                if (n < 0 && Marshal.GetLastWin32Error() == EINTR) continue;
                if (n <= 0) return false;
                sent += (int)n;
            }
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(data);
            if (cmsg != IntPtr.Zero) Marshal.FreeHGlobal(cmsg);
            Marshal.FreeHGlobal(iov);
        }
    }

    /// <summary>A client's shared memory, readable here. Zero when it can't be mapped.</summary>
    internal static IntPtr Map(int fd, int size)
    {
        var at = mmap(IntPtr.Zero, (nuint)size, ProtRead, MapShared, fd, 0);
        return at == new IntPtr(-1) ? IntPtr.Zero : at;
    }

    internal static void Unmap(IntPtr at, int size)
    {
        if (at != IntPtr.Zero) munmap(at, (nuint)size);
    }

    /// <summary>Memory as a file, holding <paramref name="bytes"/>: how the keyboard map reaches a client.</summary>
    internal static int Memory(string name, byte[] bytes)
    {
        var fd = memfd_create(name, 1 /* MFD_CLOEXEC */);
        if (fd >= 0) Write(fd, bytes);
        return fd;
    }

    internal static void Write(int fd, byte[] bytes)
    {
        var done = 0;
        while (done < bytes.Length)
        {
            var chunk = done == 0 ? bytes : bytes[done..];
            var n = write(fd, chunk, (nuint)chunk.Length);
            if (n < 0 && Marshal.GetLastWin32Error() == EINTR) continue;
            if (n <= 0) return;
            done += (int)n;
        }
    }

    /// <summary>Everything until the writer closes, at most <paramref name="limit"/> bytes.</summary>
    internal static byte[] ReadAll(int fd, int limit)
    {
        using var all = new MemoryStream();
        var buffer = new byte[65536];
        while (all.Length < limit)
        {
            var n = read(fd, buffer, (nuint)buffer.Length);
            if (n < 0 && Marshal.GetLastWin32Error() == EINTR) continue;
            if (n <= 0) break;
            all.Write(buffer, 0, (int)n);
        }
        return all.ToArray();
    }

    /// <summary>A pipe: (read end, write end).</summary>
    internal static (int read, int write) Pipe()
    {
        var fds = new int[2];
        return pipe2(fds, OCloexec) == 0 ? (fds[0], fds[1]) : (-1, -1);
    }

    // ---- libxkbcommon -------------------------------------------------------------------------

    private const string Xkb = "libxkbcommon.so.0";

    [StructLayout(LayoutKind.Sequential)]
    private struct RuleNames { public IntPtr Rules, Model, Layout, Variant, Options; }

    [DllImport(Xkb)] private static extern IntPtr xkb_context_new(int flags);
    [DllImport(Xkb)] private static extern IntPtr xkb_keymap_new_from_names(IntPtr context, ref RuleNames names, int flags);
    [DllImport(Xkb)] private static extern IntPtr xkb_keymap_get_as_string(IntPtr keymap, int format);
    [DllImport(Xkb)] internal static extern IntPtr xkb_state_new(IntPtr keymap);
    [DllImport(Xkb)] internal static extern int xkb_state_update_key(IntPtr state, uint key, int direction);
    [DllImport(Xkb)] internal static extern uint xkb_state_serialize_mods(IntPtr state, int components);
    [DllImport(Xkb)] internal static extern uint xkb_state_serialize_layout(IntPtr state, int components);

    /// <summary>The keyboard map for an xkb layout ("is", "us" …): the handle and its text.</summary>
    internal static (IntPtr keymap, string text) Keymap(string layout)
    {
        var context = xkb_context_new(0);
        if (context == IntPtr.Zero) return (IntPtr.Zero, "");
        var name = Marshal.StringToHGlobalAnsi(layout);
        try
        {
            var names = new RuleNames { Layout = name };
            var keymap = xkb_keymap_new_from_names(context, ref names, 0);
            if (keymap == IntPtr.Zero) return (IntPtr.Zero, "");
            var text = xkb_keymap_get_as_string(keymap, 1 /* XKB_KEYMAP_FORMAT_TEXT_V1 */);
            var result = Marshal.PtrToStringUTF8(text) ?? "";
            free(text);
            return (keymap, result);
        }
        finally { Marshal.FreeHGlobal(name); }
    }
}
