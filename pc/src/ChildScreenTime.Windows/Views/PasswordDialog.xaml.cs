using System.Windows;
using System.Windows.Input;

namespace ChildScreenTime.Windows.Views;

public partial class PasswordDialog : Window
{
    private readonly AppServices services;

    public PasswordDialog(AppServices services)
    {
        this.services = services;
        InitializeComponent();
        Loaded += (_, _) => PasswordInput.Focus();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e) => Authenticate();

    private void PasswordInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Authenticate();
        }
    }

    private void Authenticate()
    {
        var result = services.Authentication.Verify(PasswordInput.Password);
        PasswordInput.Clear();
        if (result.Succeeded)
        {
            DialogResult = true;
            return;
        }

        ErrorText.Text = result.Message;
        PasswordInput.Focus();
    }
}
