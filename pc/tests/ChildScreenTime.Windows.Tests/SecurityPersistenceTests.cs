using System.Text;
using ChildScreenTime.Windows.Services;

namespace ChildScreenTime.Windows.Tests;

[TestClass]
public sealed class SecurityPersistenceTests
{
    private string testRoot = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        testRoot = Path.Combine(Path.GetTempPath(), "ChildScreenTimeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (!string.IsNullOrEmpty(testRoot) && Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void DpapiStoreRoundTripsWithoutWritingPlaintext()
    {
        var path = Path.Combine(testRoot, "protected.bin");
        var store = new DpapiFileStore();
        var value = new SecretDocument { Value = "highly-sensitive-test-value" };

        store.Save(path, value);
        var rawFile = File.ReadAllBytes(path);
        var loaded = store.Load(path, () => new SecretDocument());

        Assert.AreEqual(value.Value, loaded.Value);
        Assert.IsFalse(Encoding.UTF8.GetString(rawFile).Contains(value.Value, StringComparison.Ordinal));
    }

    [TestMethod]
    public void AuthenticationLocksAfterFiveFailures()
    {
        var paths = new AppDataPaths(testRoot);
        var protectedStore = new DpapiFileStore();
        var settings = new SettingsRepository(protectedStore, paths);
        var audit = new AuditRepository(protectedStore, paths);
        var authentication = new AuthenticationService(settings, audit);
        var password = "Correct parent password 2026!";
        settings.Update(candidate =>
        {
            candidate.IsConfigured = true;
            candidate.ParentPasswordHash = AuthenticationService.CreatePasswordHash(password, password);
        });

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.IsFalse(authentication.Verify("incorrect password").Succeeded);
        }

        var fifthAttempt = authentication.Verify("incorrect password");
        var correctDuringLockout = authentication.Verify(password);

        Assert.IsNotNull(fifthAttempt.RetryAfter);
        Assert.IsFalse(correctDuringLockout.Succeeded);
        Assert.IsNotNull(correctDuringLockout.RetryAfter);
        Assert.HasCount(5, audit.GetRecent(10));
    }

    private sealed class SecretDocument
    {
        public string Value { get; set; } = string.Empty;
    }
}
