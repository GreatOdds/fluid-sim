using System;
using System.Runtime.InteropServices;

public class ByteConverter
{
    // https://github.com/jasonjmcghee/compute-shaders/blob/main/Compute/ComputeManager.cs
    // Is there a better way?

    public static byte[] ConvertToBytes<T>(T param) where T : struct
    {
        var size = Marshal.SizeOf(param);
        var arr = new byte[size];

        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(param, ptr, true);
        Marshal.Copy(ptr, arr, 0, size);
        Marshal.FreeHGlobal(ptr);

        return arr;
    }

    public static T ConvertFromBytes<T>(byte[] data) where T : struct
    {
        var size = Marshal.SizeOf<T>();

        var ptr = Marshal.AllocHGlobal(size);
        Marshal.Copy(data, 0, ptr, size);
        var output = Marshal.PtrToStructure<T>(ptr);
        Marshal.FreeHGlobal(ptr);

        return output;
    }

    public static byte[] ConvertArrayToBytes<T>(T[] param) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] arr = new byte[param.Length * size];

        // Pin the array in memory so GC won't move it, then copy
        GCHandle pin = GCHandle.Alloc(param, GCHandleType.Pinned);
        IntPtr srcPtr = pin.AddrOfPinnedObject();

        Marshal.Copy(srcPtr, arr, 0, arr.Length);

        // Don't forget to unpin when you're done
        pin.Free();

        return arr;
    }
}