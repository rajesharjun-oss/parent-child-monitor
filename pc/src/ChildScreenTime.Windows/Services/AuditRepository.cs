using ChildScreenTime.Core.Models;

namespace ChildScreenTime.Windows.Services;

public sealed class AuditRepository
{
    private const int MaximumEntries = 5000;
    private readonly object gate = new();
    private readonly DpapiFileStore store;
    private readonly string path;
    private readonly List<AuditEntry> entries;

    public AuditRepository(DpapiFileStore store, AppDataPaths paths)
    {
        this.store = store;
        path = paths.AuditFile;
        entries = store.Load(path, () => new AuditDocument()).Entries;
    }

    public void Add(string eventType, string summary)
    {
        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Audit events require a type and summary.");
        }

        lock (gate)
        {
            entries.Add(new AuditEntry
            {
                EventType = eventType[..Math.Min(eventType.Length, 64)],
                Summary = summary[..Math.Min(summary.Length, 512)]
            });
            if (entries.Count > MaximumEntries)
            {
                entries.RemoveRange(0, entries.Count - MaximumEntries);
            }

            store.Save(path, new AuditDocument { Entries = entries });
        }
    }

    public IReadOnlyList<AuditEntry> GetRecent(int count = 100)
    {
        lock (gate)
        {
            return entries
                .OrderByDescending(item => item.TimestampUtc)
                .Take(Math.Clamp(count, 1, 500))
                .Select(item => new AuditEntry
                {
                    TimestampUtc = item.TimestampUtc,
                    EventType = item.EventType,
                    Summary = item.Summary
                })
                .ToList();
        }
    }

    private sealed class AuditDocument
    {
        public List<AuditEntry> Entries { get; set; } = [];
    }
}
