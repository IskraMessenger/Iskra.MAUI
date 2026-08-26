using Iskra.Maui.Localization;
using Iskra.Maui.Services;

namespace Iskra.Maui;

/// <summary>Shows today's NLog file, refreshed on a timer.</summary>
public class LogsPage : ContentPage
{
    private const int RefreshIntervalSeconds = 5;

    private readonly Editor _logEditor = new()
    {
        IsReadOnly = true,
        FontFamily = "Courier",
        FontSize = 11,
        VerticalOptions = LayoutOptions.Fill,
        HorizontalOptions = LayoutOptions.Fill
    };

    private readonly Label _pathLabel = new()
    {
        FontSize = 11,
        TextColor = Colors.Gray,
        LineBreakMode = LineBreakMode.CharacterWrap
    };

    private readonly ToolbarItem _copyItem = new() { Order = ToolbarItemOrder.Primary, Priority = 0 };
    private readonly ToolbarItem _refreshItem = new() { Order = ToolbarItemOrder.Primary, Priority = 1 };

    private IDispatcherTimer? _refreshTimer;

    public LogsPage()
    {
        _copyItem.Command = new Command(async () => await CopyLogsAsync());
        _refreshItem.Command = new Command(RefreshLogs);
        ToolbarItems.Add(_copyItem);
        ToolbarItems.Add(_refreshItem);
        _logEditor.SetValue(Grid.RowProperty, 1);
        Content = new Grid
        {
            Padding = 12,
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            ],
            Children = { _pathLabel, _logEditor }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Title = Loc.T("logs.title");
        _copyItem.Text = Loc.T("copy");
        _refreshItem.Text = Loc.T("refresh");
        RefreshLogs();
        EnsureRefreshTimerStarted();
    }

    protected override void OnDisappearing()
    {
        if (_refreshTimer != null)
            _refreshTimer.Stop();
        base.OnDisappearing();
    }

    private void EnsureRefreshTimerStarted()
    {
        _refreshTimer ??= Dispatcher.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds);
        _refreshTimer.Tick -= OnRefreshTimerTick;
        _refreshTimer.Tick += OnRefreshTimerTick;
        if (!_refreshTimer.IsRunning)
            _refreshTimer.Start();
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        RefreshLogs();
    }

    private void RefreshLogs()
    {
        var text = AppLogReader.ReadTodayLog(out var path);
        _pathLabel.Text = path == null
            ? Loc.T("logs.path_none")
            : Loc.Tf("logs.path", path);
        _logEditor.Text = text;
    }

    private async Task CopyLogsAsync()
    {
        if (string.IsNullOrWhiteSpace(_logEditor.Text))
            return;

        await Clipboard.Default.SetTextAsync(_logEditor.Text).ConfigureAwait(true);
        await DisplayAlert(Loc.T("copied"), Loc.T("logs.copied"), Loc.T("ok")).ConfigureAwait(true);
    }
}
