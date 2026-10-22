using System.ServiceProcess;

namespace ChildScreenTime.Service;

internal sealed class AgentWindowsService : ServiceBase
{
    internal const string ServiceNameValue = "ChildScreenTimeAgent";
    private readonly SessionAgentSupervisor supervisor = new();

    public AgentWindowsService()
    {
        ServiceName = ServiceNameValue;
        CanStop = true;
        CanShutdown = true;
        CanHandleSessionChangeEvent = true;
        AutoLog = false;
    }

    protected override void OnStart(string[] args) => supervisor.Start();

    protected override void OnSessionChange(SessionChangeDescription changeDescription)
    {
        supervisor.RequestImmediateCheck();
        base.OnSessionChange(changeDescription);
    }

    protected override void OnShutdown()
    {
        supervisor.StopAsync().GetAwaiter().GetResult();
        base.OnShutdown();
    }

    protected override void OnStop() => supervisor.StopAsync().GetAwaiter().GetResult();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            supervisor.Dispose();
        }

        base.Dispose(disposing);
    }
}
