using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChildScreenTime.Windows.Services;

public sealed class DpapiFileStore
{
    private const int CryptProtectUiForbidden = 0x1;
    private const long MaxProtectedFileBytes = 32L * 1024 * 1024;

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public T Load<T>(string path, Func<T> fallbackFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(fallbackFactory);

        if (!File.Exists(path))
        {
            return fallbackFactory();
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length is <= 0 or > MaxProtectedFileBytes)
        {
            throw new InvalidDataException("Protected application data has an invalid size.");
        }

        var protectedBytes = File.ReadAllBytes(path);
        var clearBytes = Unprotect(protectedBytes);
        try
        {
            return JsonSerializer.Deserialize<T>(clearBytes, jsonOptions)
                   ?? throw new InvalidDataException("Protected application data is empty.");
        }
        finally
        {
            Array.Clear(clearBytes);
        }
    }

    public void Save<T>(string path, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(value);

        var clearBytes = JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions);
        try
        {
            var protectedBytes = Protect(clearBytes);
            var temporaryPath = path + ".tmp";
            File.WriteAllBytes(temporaryPath, protectedBytes);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            Array.Clear(clearBytes);
        }
    }

    private static byte[] Protect(byte[] clearBytes) => Transform(clearBytes, protect: true);
    private static byte[] Unprotect(byte[] protectedBytes) => Transform(protectedBytes, protect: false);

    private static byte[] Transform(byte[] input, bool protect)
    {
        var inputBlob = new DataBlob();
        var outputBlob = new DataBlob();
        try
        {
            inputBlob.Size = input.Length;
            inputBlob.Data = Marshal.AllocHGlobal(input.Length);
            Marshal.Copy(input, 0, inputBlob.Data, input.Length);

            var success = protect
                ? CryptProtectData(ref inputBlob, "Child Screen Time", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outputBlob)
                : CryptUnprotectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outputBlob);

            if (!success)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not protect application data.");
            }

            var output = new byte[outputBlob.Size];
            Marshal.Copy(outputBlob.Data, output, 0, outputBlob.Size);
            return output;
        }
        finally
        {
            if (inputBlob.Data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(inputBlob.Data);
            }

            if (outputBlob.Data != IntPtr.Zero)
            {
                LocalFree(outputBlob.Data);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
