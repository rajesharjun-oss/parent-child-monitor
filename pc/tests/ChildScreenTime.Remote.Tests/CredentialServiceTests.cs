using ChildScreenTime.Remote.Services;

namespace ChildScreenTime.Remote.Tests;

[TestClass]
public sealed class CredentialServiceTests
{
    [TestMethod]
    public void GeneratedCredentialsAreRandomAndVerifiable()
    {
        var service = new CredentialService();
        var first = service.CreateDeviceToken();
        var second = service.CreateDeviceToken();

        Assert.AreNotEqual(first, second);
        Assert.IsTrue(service.Verify(first, service.Hash(first)));
        Assert.IsFalse(service.Verify(second, service.Hash(first)));
    }
}
