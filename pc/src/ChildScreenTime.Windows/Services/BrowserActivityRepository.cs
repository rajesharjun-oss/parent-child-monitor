using System.ComponentModel;
using System.Text.Json;
using ChildScreenTime.Core.Models;

namespace ChildScreenTime.Windows.Services;

public sealed class BrowserActivityRepository
{
    private const int MaxInboxFilesPerPass = 2000;

    private readonly object gate = new();
    private readonly DpapiFileStore store;
    private readonly string path;
    private readonly string inboxDirectory;
    private readonly List<BrowserActivityRecord> records;

    public BrowserActivityRepository(DpapiFileStore store, AppDataPaths paths)
    {
        this.store = store;
        path = paths.BrowserUsageFile;
        inboxDirectory = paths.BrowserInboxDirectory;
        Directory.CreateDirectory(inboxDirectory);
        records = store.Load(path, () => new BrowserActivityDocument()).Records;
    }

    public int ImportInboxAndPrune(int retentionDays)
    {
        lock (gate)
        {
            var imported = 0;
            var dirty = false;
            var files = Directory.EnumerateFiles(inboxDirectory, "*.event", SearchOption.TopDirectoryOnly)
                .OrderBy(value => value, StringComparer.Ordinal)
                .Take(MaxInboxFilesPerPass)
                .ToArray();

            foreach (var file in files)
            {
                try
                {
                    var value = store.Load(file, () => new BrowserPlaybackEvent());
                    if (BrowserActivityPolicy.IsValidPlaybackEvent(value))
                    {
                        AddEventUnsafe(value);
                        imported++;
                        dirty = true;
                    }
                }
                catch (Exception exception) when (
                    exception is InvalidDataException or IOException or JsonException or Win32Exception)
                {
                    // Invalid or damaged inbox items are discarded without exposing their contents.
                }
                finally
                {
                    TryDelete(file);
                }
            }

            var earliest = DateOnly.FromDateTime(DateTime.Today.AddDays(-(retentionDays - 1)));
            if (records.RemoveAll(item => item.LocalDate < earliest) > 0)
            {
                dirty = true;
            }

            if (dirty)
            {
                store.Save(path, new BrowserActivityDocument { Records = records });
            }

            return imported;
        }
    }

    public IReadOnlyList<BrowserActivityRecord> GetAll()
    {
        lock (gate)
        {
            return records.Select(Clone).ToList();
        }
    }

    public void DeleteAll()
    {
        lock (gate)
        {
            records.Clear();
            store.Save(path, new BrowserActivityDocument());
            foreach (var file in Directory.EnumerateFiles(inboxDirectory, "*.event", SearchOption.TopDirectoryOnly))
            {
                TryDelete(file);
            }
        }
    }

    private void AddEventUnsafe(BrowserPlaybackEvent value)
    {
        var record = records.FirstOrDefault(item =>
            item.LocalDate == value.LocalDate &&
            string.Equals(item.VideoId, value.VideoId, StringComparison.Ordinal));

        if (record is null)
        {
            record = new BrowserActivityRecord
            {
                LocalDate = value.LocalDate,
                VideoId = value.VideoId,
                Title = value.Title,
                Channel = value.Channel,
                LastSeenUtc = value.LastSeenUtc
            };
            records.Add(record);
        }

        record.WatchedSeconds = Math.Min(86400, record.WatchedSeconds + value.WatchedSeconds);
        record.LastSeenUtc = value.LastSeenUtc > record.LastSeenUtc ? value.LastSeenUtc : record.LastSeenUtc;
        record.Title = value.Title;
        record.Channel = value.Channel;
    }

    private static BrowserActivityRecord Clone(BrowserActivityRecord source) => new()
    {
        LocalDate = source.LocalDate,
        VideoId = source.VideoId,
        Title = source.Title,
        Channel = source.Channel,
        WatchedSeconds = source.WatchedSeconds,
        LastSeenUtc = source.LastSeenUtc
    };

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class BrowserActivityDocument
    {
        public List<BrowserActivityRecord> Records { get; set; } = [];
    }
}
