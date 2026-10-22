using System.Globalization;
using System.Text;
using ChildScreenTime.Core.Models;

namespace ChildScreenTime.Windows.Services;

public static class CsvExportService
{
    public static void Export(string path, IEnumerable<UsageRecord> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(records);

        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine("Date,Application,Process,Active minutes,Last seen UTC");
        foreach (var record in records.OrderByDescending(item => item.LocalDate).ThenByDescending(item => item.ActiveSeconds))
        {
            writer.WriteLine(string.Join(",",
                Escape(record.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Escape(record.DisplayName),
                Escape(record.ProcessName),
                Math.Round(record.ActiveSeconds / 60d, 1).ToString(CultureInfo.InvariantCulture),
                Escape(record.LastSeenUtc.ToString("O", CultureInfo.InvariantCulture))));
        }
    }

    private static string Escape(string value)
    {
        var safeValue = value.ReplaceLineEndings(" ").Replace('\t', ' ').Trim();
        if (safeValue.Length > 0 && safeValue[0] is '=' or '+' or '-' or '@')
        {
            safeValue = "'" + safeValue;
        }

        return $"\"{safeValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
