using ChildScreenTime.Core.Models;
using ChildScreenTime.Core.Services;

namespace ChildScreenTime.Core.Tests;

[TestClass]
public sealed class RemoteEndpointValidatorTests
{
    [TestMethod]
    public void RequiresHttpsOutsideLoopback()
    {
        var settings = new RemoteSyncSettings { ServerBaseUrl = "http://example.com", DeviceName = "Child PC" };
        Assert.IsNotEmpty(RemoteEndpointValidator.Validate(settings));
        Assert.IsEmpty(RemoteEndpointValidator.Validate(new RemoteSyncSettings { ServerBaseUrl = "http://localhost:8080", DeviceName = "Child PC" }));
    }

    [TestMethod]
    public void RejectsPartialOrMalformedCredentials()
    {
        var settings = new RemoteSyncSettings
        {
            ServerBaseUrl = "https://monitor.example.com",
            DeviceName = "Child PC",
            DeviceId = Guid.NewGuid().ToString("D"),
            DeviceToken = "short"
        };

        Assert.IsNotEmpty(RemoteEndpointValidator.Validate(settings));
    }
}
