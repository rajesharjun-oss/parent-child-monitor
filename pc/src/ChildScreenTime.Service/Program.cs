using System.ServiceProcess;

namespace ChildScreenTime.Service;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            return 1;
        }

        var runInConsole = args.Any(argument =>
            string.Equals(argument, "--console", StringComparison.OrdinalIgnoreCase));

        if (runInConsole)
        {
            using var supervisor = new SessionAgentSupervisor();
            supervisor.Start();
            Console.WriteLine("Child Screen Time service supervisor is running. Press Ctrl+C to stop.");
            using var stopped = new ManualResetEventSlim();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                stopped.Set();
            };
            stopped.Wait();
            supervisor.StopAsync().GetAwaiter().GetResult();
            return 0;
        }

        ServiceBase.Run(new AgentWindowsService());
        return 0;
    }
}
