using ChildScreenTime.Core.Models;

namespace ChildScreenTime.Windows.Services;

public sealed class UsageRepository
{
    private readonly object gate = new();
    private readonly DpapiFileStore store;
    private readonly string path;
    private readonly List<UsageRecord> records;
    private bool dirty;

    public UsageRepository(DpapiFileStore store, AppDataPaths paths)
    {
        this.store = store;
        path = paths.UsageFile;
        records = store.Load(path, () => new UsageDocument()).Records;
    }

    public void AddSample(DateOnly date, ForegroundApplication application, int seconds)
    {
        if (seconds <= 0 || string.IsNullOrWhiteSpace(application.ProcessName))
        {
            return;
        }

        lock (gate)
        {
            var record = records.FirstOrDefault(item =>
                item.LocalDate == date &&
                string.Equals(item.ProcessName, application.ProcessName, StringComparison.OrdinalIgnoreCase));

            if (record is null)
            {
                record = new UsageRecord
                {
                    LocalDate = date,
                    ProcessName = application.ProcessName,
                    DisplayName = application.DisplayName
                };
                records.Add(record);
            }

            record.ActiveSeconds += seconds;
            record.LastSeenUtc = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(application.DisplayName))
            {
                record.DisplayName = application.DisplayName;
            }

            dirty = true;
        }
    }

    public IReadOnlyList<UsageRecord> GetForDate(DateOnly date)
    {
        lock (gate)
        {
            return records
                .Where(item => item.LocalDate == date)
                .OrderByDescending(item => item.ActiveSeconds)
                .Select(Clone)
                .ToList();
        }
    }

    public long GetTotalSeconds(DateOnly date) => GetForDate(date).Sum(item => item.ActiveSeconds);

    public long GetProcessSeconds(DateOnly date, string processName) => GetForDate(date)
        .Where(item => string.Equals(item.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
        .Sum(item => item.ActiveSeconds);

    public IReadOnlyList<UsageRecord> GetAll()
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
            store.Save(path, new UsageDocument());
            dirty = false;
        }
    }

    public void FlushAndPrune(int retentionDays)
    {
        lock (gate)
        {
            var earliest = DateOnly.FromDateTime(DateTime.Today.AddDays(-(retentionDays - 1)));
            if (records.RemoveAll(item => item.LocalDate < earliest) > 0)
            {
                dirty = true;
            }

            if (!dirty)
            {
                return;
            }

            store.Save(path, new UsageDocument { Records = records });
            dirty = false;
        }
    }

    private static UsageRecord Clone(UsageRecord source) => new()
    {
        LocalDate = source.LocalDate,
        ProcessName = source.ProcessName,
        DisplayName = source.DisplayName,
        ActiveSeconds = source.ActiveSeconds,
        LastSeenUtc = source.LastSeenUtc
    };

    private sealed class UsageDocument
    {
        public List<UsageRecord> Records { get; set; } = [];
    }
}
