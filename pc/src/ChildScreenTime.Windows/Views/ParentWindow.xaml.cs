using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using ChildScreenTime.Core.Models;
using ChildScreenTime.Core.Services;
using ChildScreenTime.Windows.Services;
using Microsoft.Win32;

namespace ChildScreenTime.Windows.Views;

public partial class ParentWindow : Window
{
    private readonly AppServices services;
    private readonly ObservableCollection<AppRule> rules = [];

    public ParentWindow(AppServices services)
    {
        this.services = services;
        InitializeComponent();
        RulesGrid.ItemsSource = rules;
        LoadSettings();
        LoadActivity();
        LoadAudit();
        LoadRemoteSettings();
        services.RemoteSync.StatusChanged += RemoteSync_StatusChanged;
        Closed += (_, _) => services.RemoteSync.StatusChanged -= RemoteSync_StatusChanged;
    }

    private void LoadSettings()
    {
        var settings = services.Settings.GetSnapshot();
        MonitoringEnabled.IsChecked = settings.MonitoringEnabled;
        DailyLimit.Text = settings.DailyLimitMinutes.ToString(CultureInfo.InvariantCulture);
        BedtimeEnabled.IsChecked = settings.BedtimeEnabled;
        BedtimeStart.Text = settings.BedtimeStart.ToString("HH:mm", CultureInfo.InvariantCulture);
        BedtimeEnd.Text = settings.BedtimeEnd.ToString("HH:mm", CultureInfo.InvariantCulture);
        RetentionDays.Text = settings.RetentionDays.ToString(CultureInfo.InvariantCulture);
        rules.Clear();
        foreach (var rule in settings.AppRules)
        {
            rules.Add(new AppRule
            {
                ProcessName = rule.ProcessName,
                DisplayName = rule.DisplayName,
                DailyLimitMinutes = rule.DailyLimitMinutes,
                AlwaysBlocked = rule.AlwaysBlocked
            });
        }
    }

    private void LoadActivity()
    {
        var records = services.Usage.GetAll();
        UsageGrid.ItemsSource = records
            .OrderByDescending(item => item.LocalDate)
            .ThenByDescending(item => item.ActiveSeconds)
            .Select(item => new UsageRow(
                item.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(item.DisplayName) ? item.ProcessName : item.DisplayName,
                item.ProcessName,
                Math.Round(item.ActiveSeconds / 60d, 1),
                item.LastSeenUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)))
            .ToList();

        var today = DateOnly.FromDateTime(DateTime.Today);
        TodayTotalText.Text = $"{Math.Round(services.Usage.GetTotalSeconds(today) / 60d, 1)} active minutes";
    }

    private void LoadAudit()
    {
        AuditGrid.ItemsSource = services.Audit.GetRecent(200)
            .Select(item => new AuditRow(
                item.TimestampUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                item.EventType.Replace('_', ' '),
                item.Summary))
            .ToList();
    }

    private void LoadRemoteSettings()
    {
        var remote = services.Settings.GetSnapshot().RemoteSync;
        RemoteServerUrl.Text = remote.ServerBaseUrl;
        RemoteDeviceName.Text = string.IsNullOrWhiteSpace(remote.DeviceName) ? Environment.MachineName : remote.DeviceName;
        UpdateRemoteStatus(services.RemoteSync.Status);
    }

    private void RemoteSync_StatusChanged(object? sender, RemoteSyncStatus status) =>
        Dispatcher.Invoke(() => UpdateRemoteStatus(status));

    private void UpdateRemoteStatus(RemoteSyncStatus status)
    {
        RemoteStatusText.Text = status.Message;
        RemoteStatusText.Foreground = status.IsPaired ? System.Windows.Media.Brushes.SeaGreen : System.Windows.Media.Brushes.DimGray;
        RemoteLastSyncText.Text = status.LastSuccessUtc is null
            ? "No successful remote synchronization yet."
            : $"Last successful sync: {status.LastSuccessUtc.Value.ToLocalTime():g}";
        PairRemoteButton.IsEnabled = !status.IsSyncing;
        SyncNowButton.IsEnabled = status.IsPaired && !status.IsSyncing;
        DisconnectRemoteButton.IsEnabled = status.IsPaired && !status.IsSyncing;
        RemoteServerUrl.IsEnabled = !status.IsPaired;
        RemoteDeviceName.IsEnabled = !status.IsPaired;
        RemotePairingCode.IsEnabled = !status.IsPaired;
    }

    private async void PairRemote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PairRemoteButton.IsEnabled = false;
            RemoteStatusText.Text = "Pairing securely...";
            await services.RemoteSync.PairAsync(RemoteServerUrl.Text, RemotePairingCode.Text, RemoteDeviceName.Text);
            RemotePairingCode.Clear();
            LoadAudit();
            UpdateRemoteStatus(services.RemoteSync.Status);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or HttpRequestException or InvalidDataException)
        {
            RemoteStatusText.Foreground = System.Windows.Media.Brushes.Firebrick;
            RemoteStatusText.Text = exception.Message;
            PairRemoteButton.IsEnabled = true;
        }
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        await services.RemoteSync.SyncNowAsync();
        UpdateRemoteStatus(services.RemoteSync.Status);
    }

    private void DisconnectRemote_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = System.Windows.MessageBox.Show(
            this,
            "Disconnect this PC from remote viewing? Existing dashboard records remain until the parent deletes or revokes the device on the server.",
            "Disconnect remote viewing",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        services.RemoteSync.Unpair();
        RemoteServerUrl.Clear();
        RemotePairingCode.Clear();
        LoadRemoteSettings();
        LoadAudit();
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        var normalizedName = RestrictionEvaluator.NormalizeProcessName(NewProcessName.Text);
        int? limit = null;
        if (!string.IsNullOrWhiteSpace(NewRuleLimit.Text))
        {
            if (!int.TryParse(NewRuleLimit.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedLimit))
            {
                SetSaveMessage("Application limit must be a whole number.", isError: true);
                return;
            }
            limit = parsedLimit;
        }

        var candidate = new AppSettings
        {
            IsConfigured = true,
            ParentPasswordHash = "validation-placeholder",
            AppRules = [new AppRule { ProcessName = normalizedName, DisplayName = NewDisplayName.Text.Trim(), DailyLimitMinutes = limit }]
        };
        var errors = SettingsValidator.Validate(candidate);
        if (errors.Count > 0)
        {
            SetSaveMessage(string.Join(" ", errors), isError: true);
            return;
        }

        if (rules.Any(rule => string.Equals(RestrictionEvaluator.NormalizeProcessName(rule.ProcessName), normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            SetSaveMessage("A rule for that process already exists.", isError: true);
            return;
        }

        rules.Add(candidate.AppRules[0]);
        NewProcessName.Clear();
        NewDisplayName.Clear();
        NewRuleLimit.Clear();
        SetSaveMessage("Rule added. Select Save controls to apply it.", isError: false);
    }

    private void RemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is AppRule selected)
        {
            rules.Remove(selected);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var errors = new List<string>();
        if (!int.TryParse(DailyLimit.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var dailyLimit))
        {
            errors.Add("Daily limit must be a whole number.");
        }

        if (!int.TryParse(RetentionDays.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var retentionDays))
        {
            errors.Add("Retention must be a whole number.");
        }

        var startsAtValidTime = TimeOnly.TryParseExact(BedtimeStart.Text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var bedtimeStart);
        var endsAtValidTime = TimeOnly.TryParseExact(BedtimeEnd.Text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var bedtimeEnd);
        if (!startsAtValidTime || !endsAtValidTime)
        {
            errors.Add("Bedtime values must use 24-hour HH:mm format.");
        }

        var current = services.Settings.GetSnapshot();
        var candidate = new AppSettings
        {
            IsConfigured = true,
            ParentPasswordHash = current.ParentPasswordHash,
            MonitoringEnabled = MonitoringEnabled.IsChecked == true,
            DailyLimitMinutes = dailyLimit,
            BedtimeEnabled = BedtimeEnabled.IsChecked == true,
            BedtimeStart = bedtimeStart,
            BedtimeEnd = bedtimeEnd,
            RetentionDays = retentionDays,
            AppRules = rules.ToList()
        };
        errors.AddRange(SettingsValidator.Validate(candidate));
        if (errors.Count > 0)
        {
            SetSaveMessage(string.Join(" ", errors.Distinct(StringComparer.Ordinal)), isError: true);
            return;
        }

        var wasMonitoring = current.MonitoringEnabled;
        services.Settings.Update(settings =>
        {
            settings.MonitoringEnabled = candidate.MonitoringEnabled;
            settings.DailyLimitMinutes = candidate.DailyLimitMinutes;
            settings.BedtimeEnabled = candidate.BedtimeEnabled;
            settings.BedtimeStart = candidate.BedtimeStart;
            settings.BedtimeEnd = candidate.BedtimeEnd;
            settings.RetentionDays = candidate.RetentionDays;
            settings.AppRules = candidate.AppRules;
        });
        services.Usage.FlushAndPrune(retentionDays);
        services.BrowserActivity.ImportInboxAndPrune(retentionDays);
        services.Audit.Add("controls_updated", "Screen-time controls and application rules were updated.");
        if (wasMonitoring != candidate.MonitoringEnabled)
        {
            services.Audit.Add(candidate.MonitoringEnabled ? "monitoring_resumed" : "monitoring_paused", candidate.MonitoringEnabled
                ? "A parent resumed monitoring."
                : "A parent paused monitoring.");
        }
        SetSaveMessage("Controls saved securely.", isError: false);
        LoadAudit();
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var verification = services.Authentication.Verify(CurrentPassword.Password);
        CurrentPassword.Clear();
        if (!verification.Succeeded)
        {
            PasswordMessage.Foreground = System.Windows.Media.Brushes.Firebrick;
            PasswordMessage.Text = verification.Message;
            return;
        }

        try
        {
            var newHash = AuthenticationService.CreatePasswordHash(NewPassword.Password, ConfirmNewPassword.Password);
            services.Settings.Update(settings => settings.ParentPasswordHash = newHash);
            services.Audit.Add("parent_password_changed", "The parent password was changed.");
            NewPassword.Clear();
            ConfirmNewPassword.Clear();
            PasswordMessage.Foreground = System.Windows.Media.Brushes.SeaGreen;
            PasswordMessage.Text = "Parent password changed successfully.";
            LoadAudit();
        }
        catch (ArgumentException exception)
        {
            PasswordMessage.Foreground = System.Windows.Media.Brushes.Firebrick;
            PasswordMessage.Text = exception.Message;
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Child Screen Time usage",
            Filter = "CSV file (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = $"child-screen-time-{DateTime.Today:yyyy-MM-dd}.csv"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        CsvExportService.Export(dialog.FileName, services.Usage.GetAll());
        services.Audit.Add("usage_exported", "A parent exported an application-usage CSV file.");
        SetSaveMessage("Usage exported successfully.", isError: false);
        LoadAudit();
    }

    private void DeleteHistory_Click(object sender, RoutedEventArgs e)
    {
        var authentication = new PasswordDialog(services) { Owner = this };
        if (authentication.ShowDialog() != true)
        {
            return;
        }

        var confirmation = System.Windows.MessageBox.Show(
            this,
            "Delete all recorded application usage and YouTube watch activity? This cannot be undone.",
            "Delete activity history",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        services.Usage.DeleteAll();
        services.BrowserActivity.DeleteAll();
        services.Audit.Add("usage_deleted", "A parent permanently deleted all application usage and YouTube watch activity.");
        LoadActivity();
        LoadAudit();
        SetSaveMessage("Activity history deleted permanently.", isError: false);
    }

    private void SetSaveMessage(string message, bool isError)
    {
        SaveMessage.Foreground = isError ? System.Windows.Media.Brushes.Firebrick : System.Windows.Media.Brushes.SeaGreen;
        SaveMessage.Text = message;
    }

    private sealed record UsageRow(string Date, string Application, string Process, double Minutes, string LastSeen);
    private sealed record AuditRow(string Time, string Event, string Summary);
}
