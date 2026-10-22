using System.Globalization;
using System.Windows;
using ChildScreenTime.Core.Services;
using ChildScreenTime.Windows.Services;

namespace ChildScreenTime.Windows.Views;

public partial class SetupWindow : Window
{
    private readonly AppServices services;

    public SetupWindow(AppServices services)
    {
        this.services = services;
        InitializeComponent();
    }

    private void CompleteSetup_Click(object sender, RoutedEventArgs e)
    {
        var errors = SettingsValidator.ValidateNewPassword(ParentPassword.Password, ConfirmPassword.Password).ToList();
        if (!int.TryParse(DailyLimit.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var dailyLimit) || dailyLimit is < 1 or > 1440)
        {
            errors.Add("Daily limit must be a whole number between 1 and 1,440.");
        }

        var startsAtValidTime = TimeOnly.TryParseExact(BedtimeStart.Text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var bedtimeStart);
        var endsAtValidTime = TimeOnly.TryParseExact(BedtimeEnd.Text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var bedtimeEnd);
        if (!startsAtValidTime || !endsAtValidTime)
        {
            errors.Add("Bedtime values must use 24-hour HH:mm format.");
        }

        if (errors.Count > 0)
        {
            ErrorText.Text = string.Join(Environment.NewLine, errors);
            return;
        }

        try
        {
            var hash = AuthenticationService.CreatePasswordHash(ParentPassword.Password, ConfirmPassword.Password);
            services.Settings.Update(settings =>
            {
                settings.IsConfigured = true;
                settings.ParentPasswordHash = hash;
                settings.DailyLimitMinutes = dailyLimit;
                settings.BedtimeEnabled = BedtimeEnabled.IsChecked == true;
                settings.BedtimeStart = bedtimeStart;
                settings.BedtimeEnd = bedtimeEnd;
            });
            ParentPassword.Clear();
            ConfirmPassword.Clear();
            services.Audit.Add("setup_completed", "Initial parent controls were configured.");
            DialogResult = true;
        }
        catch (Exception exception)
        {
            ErrorText.Text = "Setup could not be saved securely: " + exception.Message;
        }
    }
}
