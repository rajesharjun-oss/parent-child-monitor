using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ChildScreenTime.Core.Models;

namespace ChildScreenTime.Windows.Views;

public partial class BlockWindow : Window
{
    private readonly AppServices services;
    private bool mayClose;

    public BlockWindow(AppServices services, bool isPrimaryScreen)
    {
        this.services = services;
        InitializeComponent();
        ParentAccessButton.Visibility = isPrimaryScreen ? Visibility.Visible : Visibility.Collapsed;
        PreviewKeyDown += BlockWindow_PreviewKeyDown;
    }

    public void UpdateDecision(RestrictionDecision decision) => ReasonText.Text = decision.Message;

    public void AllowClose() => mayClose = true;

    public void PositionToScreen(int left, int top, int width, int height)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _ = SetWindowPos(handle, new IntPtr(-1), left, top, width, height, 0x0040);
    }

    private void ParentAccess_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PasswordDialog(services) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        services.Settings.Update(settings => settings.TemporaryOverrideUntilUtc = DateTimeOffset.UtcNow.AddMinutes(15));
        services.Audit.Add("temporary_override", "A parent granted a 15-minute temporary override.");
    }

    private static void BlockWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F4 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            e.Handled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!mayClose)
        {
            e.Cancel = true;
        }
        base.OnClosing(e);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
