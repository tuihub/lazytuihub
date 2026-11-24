using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static unsafe class Provider
{
    [UnmanagedCallersOnly(EntryPoint="process_chunk", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int Process(byte* data, int size, delegate* unmanaged[Cdecl]<int,int> ack)
    {
        int pid = Environment.ProcessId;
        int delta = ack(pid);
        for (int i=0; i<size; i++) data[i] += (byte)delta;
        return RuntimeFeature.IsDynamicCodeSupported ? -1 : pid;
    }
}