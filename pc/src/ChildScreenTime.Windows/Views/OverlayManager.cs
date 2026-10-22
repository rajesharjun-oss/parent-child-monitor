using System.Windows;
using ChildScreenTime.Core.Models;
using Forms = System.Windows.Forms;

namespace ChildScreenTime.Windows.Views;

public sealed class OverlayManager : IDisposable
{
    private readonly AppServices services;
    private readonly List<BlockWindow> windows = [];

    public OverlayManager(AppServices services)
    {
        this.services = services;
    }

    public void Update(RestrictionDecision decision)
    {
        if (!decision.IsBlocked)
        {
            CloseAll();
            return;
        }

        if (windows.Count == 0)
        {
            foreach (var screen in Forms.Screen.AllScreens)
            {
                var window = new BlockWindow(services, screen.Primary);
                window.UpdateDecision(decision);
                windows.Add(window);
                window.Show();
                window.PositionToScreen(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height);
            }
        }
        else
        {
            foreach (var window in windows)
            {
                window.UpdateDecision(decision);
            }
        }
    }

    private void CloseAll()
    {
        foreach (var window in windows)
        {
            window.AllowClose();
            window.Close();
        }
        windows.Clear();
    }

    public void Dispose() => CloseAll();
}
