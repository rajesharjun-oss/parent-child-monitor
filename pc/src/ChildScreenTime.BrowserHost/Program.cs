using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using ChildScreenTime.Core.Models;

namespace ChildScreenTime.BrowserHost;

internal static class Program
{
    private const int MaxMessageBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<int> Main()
    {
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();
        var writer = new ProtectedEventWriter();

        try
        {
            while (true)
            {
                var lengthBuffer = new byte[4];
                if (!await ReadExactlyOrEofAsync(input, lengthBuffer).ConfigureAwait(false))
                {
                    return 0;
                }

                var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
                if (length is <= 0 or > MaxMessageBytes)
                {
                    return 2;
                }

                var payload = new byte[length];
                if (!await ReadExactlyOrEofAsync(input, payload).ConfigureAwait(false))
                {
                    return 2;
                }

                NativeMessage? message;
                try
                {
                    message = JsonSerializer.Deserialize<NativeMessage>(payload, JsonOptions);
                }
                catch (JsonException)
                {
                    await WriteResponseAsync(output, false, "invalid_message").ConfigureAwait(false);
                    continue;
                }

                if (!TryCreateEvent(message, out var playbackEvent))
                {
                    await WriteResponseAsync(output, false, "invalid_message").ConfigureAwait(false);
                    continue;
                }

                try
                {
                    writer.Write(playbackEvent);
                    await WriteResponseAsync(output, true, null).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or Win32Exception)
                {
                    await WriteResponseAsync(output, false, "storage_unavailable").ConfigureAwait(false);
                }
            }
        }
        catch (IOException)
        {
            return 1;
        }
    }

    private static bool TryCreateEvent(NativeMessage? message, out BrowserPlaybackEvent playbackEvent)
    {
        playbackEvent = new BrowserPlaybackEvent();
        if (message is null ||
            !string.Equals(message.Type, "youtubePlayback", StringComparison.Ordinal) ||
            !BrowserActivityPolicy.IsValidVideoId(message.VideoId) ||
            !BrowserActivityPolicy.IsSafeText(message.Title, 1, 200) ||
            !BrowserActivityPolicy.IsSafeText(message.Channel, 0, 120) ||
            message.WatchedSeconds is < 1 or > 30)
        {
            return false;
        }

        playbackEvent = new BrowserPlaybackEvent
        {
            LocalDate = DateOnly.FromDateTime(DateTime.Today),
            VideoId = message.VideoId!,
            Title = message.Title!.Trim(),
            Channel = message.Channel!.Trim(),
            WatchedSeconds = message.WatchedSeconds,
            LastSeenUtc = DateTimeOffset.UtcNow
        };
        return true;
    }

    private static async Task<bool> ReadExactlyOrEofAsync(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset)).ConfigureAwait(false);
            if (read == 0)
            {
                return offset == 0;
            }
            offset += read;
        }
        return true;
    }

    private static async Task WriteResponseAsync(Stream output, bool ok, string? error)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new NativeResponse(ok, error), JsonOptions);
        var lengthBuffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, payload.Length);
        await output.WriteAsync(lengthBuffer).ConfigureAwait(false);
        await output.WriteAsync(payload).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }

    private sealed class NativeMessage
    {
        public string? Type { get; set; }
        public string? VideoId { get; set; }
        public string? Title { get; set; }
        public string? Channel { get; set; }
        public int WatchedSeconds { get; set; }
    }

    private sealed record NativeResponse(bool Ok, string? Error);
}

internal sealed class ProtectedEventWriter
{
    private const int CryptProtectUiForbidden = 0x1;
    private readonly string inboxDirectory;
    private readonly JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ProtectedEventWriter()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChildScreenTime");
        inboxDirectory = Path.Combine(root, "browser-inbox");
        Directory.CreateDirectory(inboxDirectory);
    }

    public void Write(BrowserPlaybackEvent value)
    {
        var clearBytes = JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions);
        try
        {
            var protectedBytes = Protect(clearBytes);
            var fileName = $"{DateTimeOffset.UtcNow.UtcTicks:D19}-{Guid.NewGuid():N}.event";
            var finalPath = Path.Combine(inboxDirectory, fileName);
            var temporaryPath = finalPath + ".tmp";
            File.WriteAllBytes(temporaryPath, protectedBytes);
            File.Move(temporaryPath, finalPath);
        }
        finally
        {
            Array.Clear(clearBytes);
        }
    }

    private static byte[] Protect(byte[] clearBytes)
    {
        var inputBlob = new DataBlob();
        var outputBlob = new DataBlob();
        try
        {
            inputBlob.Size = clearBytes.Length;
            inputBlob.Data = Marshal.AllocHGlobal(clearBytes.Length);
            Marshal.Copy(clearBytes, 0, inputBlob.Data, clearBytes.Length);

            if (!CryptProtectData(
                    ref inputBlob,
                    "Child Screen Time browser activity",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out outputBlob))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not protect browser activity.");
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

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
