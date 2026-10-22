using ChildScreenTime.Core.Services;

namespace ChildScreenTime.Core.Tests;

[TestClass]
public sealed class AgentLaunchPolicyTests
{
    [TestMethod]
    public void DoesNotLaunchWithoutAnInteractiveWindowsSession()
    {
        var result = AgentLaunchPolicy.ShouldLaunch(AgentLaunchPolicy.NoActiveSession, []);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void LaunchesWhenTheActiveSessionHasNoAgent()
    {
        var result = AgentLaunchPolicy.ShouldLaunch(4, [1, 2]);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void DoesNotLaunchASecondAgentInTheActiveSession()
    {
        var result = AgentLaunchPolicy.ShouldLaunch(4, [2, 4]);

        Assert.IsFalse(result);
    }
}
