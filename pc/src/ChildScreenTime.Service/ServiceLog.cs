using System.Text;

namespace ChildScreenTime.Service;

internal sealed class ServiceLog
{
    private const long MaximumLogBytes = 2L * 1024 * 1024;
    private readonly object gate = new();
    private readonly string path;

    public ServiceLog()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ChildScreenTime");
        Directory.CreateDirectory(root);
        path = Path.Combine(root, "service.log");
    }

    public void Information(string message) => Write("INFO", message);

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        try
        {
            lock (gate)
            {
                var safeMessage = message.ReplaceLineEndings(" ");
                var line = $"{DateTimeOffset.UtcNow:O}\t{level}\t{safeMessage}{Environment.NewLine}";
                if (File.Exists(path) && new FileInfo(path).Length > MaximumLogBytes)
                {
                    File.WriteAllText(path, line, Encoding.UTF8);
                }
                else
                {
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
            }
        }
        catch
        {
            // A logging failure must never stop supervision.
        }
    }
}
