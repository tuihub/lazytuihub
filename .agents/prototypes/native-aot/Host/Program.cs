using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

unsafe class Host
{
    static int callbacks;
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static int Ack(int pid) { callbacks++; return pid == Environment.ProcessId ? 17 : -1; }
    static int Main(string[] args)
    {
        var handle = NativeLibrary.Load(Path.GetFullPath(args[0]));
        var symbol = NativeLibrary.GetExport(handle, "process_chunk");
        var process = (delegate* unmanaged[Cdecl]<byte*, int, delegate* unmanaged[Cdecl]<int,int>, int>)symbol;
        Span<byte> data = stackalloc byte[] { 2, 3 };
        int pid;
        fixed (byte* p = data) { pid = process(p, data.Length, &Ack); }
        Console.WriteLine($"host={Environment.ProcessId} plugin={pid} callback={callbacks} buffer={data[0]},{data[1]} dynamic={RuntimeFeature.IsDynamicCodeSupported}");
        return pid == Environment.ProcessId && callbacks == 1 && data[0] == 19 && data[1] == 20 && !RuntimeFeature.IsDynamicCodeSupported ? 0 : 1;
    }
}