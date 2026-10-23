using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dwarpal.App.Services;
using Dwarpal.Core.Models;
using Dwarpal.Core.Services;
using Serilog;

namespace Dwarpal.App.ViewModels;

public sealed class ScheduleRow
{
    public ScheduleRow(ProtectionSchedule schedule)
    {
        Id = schedule.Id;
        Name = schedule.Name;
        DaysText = schedule.DaysText;
        TimeRangeText = schedule.TimeRangeText;
        IsEnabled = schedule.IsEnabled;
    }

    public int Id { get; }
    public string Name { get; }
    public string DaysText { get; }
    public string TimeRangeText { get; }
    public bool IsEnabled { get; }
    public string Summary => $"{DaysText} · {TimeRangeText}";
}

public partial class DayOption : ObservableObject
{
    public DayOption(string label, int bit, bool isSelected)
    {
        Label = label;
        Bit = bit;
        IsSelected = isSelected;
    }

    public string Label { get; }
    public int Bit { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public partial class ScheduleViewModel : ObservableObject
{
    private static readonly string[] DayLabels = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    [ObservableProperty]
    private string _newName = "Protection hours";

    [ObservableProperty]
    private string _startTime = "09:00";

    [ObservableProperty]
    private string _endTime = "18:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWindows), nameof(HasNoWindows))]
    private bool _hasAnyWindows;

    [ObservableProperty]
    private string _stateText = "Protection is always on";

    [ObservableProperty]
    private string _stateDetail = "Kono schedule window nei — sob somoy protection active.";

    [ObservableProperty]
    private bool _isScheduleOpen = true;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isBusy;

    public bool HasWindows => HasAnyWindows;
    public bool HasNoWindows => !HasAnyWindows;

    public ObservableCollection<ScheduleRow> Windows { get; } = new();

    public ObservableCollection<DayOption> Days { get; } = new(
        DayLabels.Select((label, index) => new DayOption(label, 1 << index, index is >= 1 and <= 5)));

    public event EventHandler? ScheduleChanged;

    public async Task LoadAsync()
    {
        try
        {
            var schedules = await AppServices.Schedules.GetAllAsync();
            var now = DateTimeOffset.Now;

            Windows.Clear();

            foreach (var schedule in schedules)
            {
                Windows.Add(new ScheduleRow(schedule));
            }

            HasAnyWindows = Windows.Count > 0;

            var open = SchedulePolicy.IsProtectionActive(schedules, now);
            IsScheduleOpen = open;

            if (!SchedulePolicy.HasEnabledWindow(schedules))
            {
                StateText = "Protection is always on";
                StateDetail = "Kono enabled window nei — sob somoy protection active.";
            }
            else if (open)
            {
                StateText = "Protection is active now";
                StateDetail = "Ei somoy protected app open korle lock screen ashbe.";
            }
            else
            {
                var next = SchedulePolicy.TimeUntilNextWindow(schedules, now);

                StateText = "Protection is paused now";
                StateDetail = next is null
                    ? "Porer window er somoy ashle abar protection chalu hobe."
                    : $"Porer window {FormatDuration(next.Value)} por shuru hobe.";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Schedule load failed");
            SetStatus("Schedule load korte parlam na: " + ex.Message, isError: true);
        }
    }

    public void ShiftStart(int steps) => StartTime = Shift(StartTime, steps);

    public void ShiftEnd(int steps) => EndTime = Shift(EndTime, steps);

    public async Task AddAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var daysMask = Days.Where(day => day.IsSelected).Aggregate(0, (mask, day) => mask | day.Bit);
        var start = ParseMinutes(StartTime);
        var end = ParseMinutes(EndTime);

        if (start is null || end is null)
        {
            SetStatus("Start/end somoy thik nei.", isError: true);
            return;
        }

        IsBusy = true;

        try
        {
            var created = await AppServices.Schedules.AddAsync(NewName, daysMask, start.Value, end.Value);

            if (AppServices.Protection is not null)
            {
                await AppServices.Protection.RefreshAsync(lockRunningInstances: true);
            }

            await AppServices.ActivityLog.LogAsync(
                ActivityEventType.Settings,
                ActivityOutcome.Success,
                $"Protection window '{created.Name}' added ({created.DaysText}, {created.TimeRangeText}).");

            await LoadAsync();
            SetStatus($"'{created.Name}' window added.", isError: false);
            ScheduleChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (ArgumentException ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Adding schedule window failed");
            SetStatus("Window add korte parlam na: " + ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SetEnabledAsync(ScheduleRow row, bool enabled)
    {
        try
        {
            await AppServices.Schedules.SetEnabledAsync(row.Id, enabled);

            if (AppServices.Protection is not null)
            {
                await AppServices.Protection.RefreshAsync(lockRunningInstances: enabled);
            }

            await AppServices.ActivityLog.LogAsync(
                ActivityEventType.Settings,
                ActivityOutcome.Success,
                $"Protection window '{row.Name}' {(enabled ? "enabled" : "paused")}.");

            await LoadAsync();
            SetStatus($"{row.Name} {(enabled ? "enabled" : "paused")}.", isError: false);
            ScheduleChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Toggling schedule window failed");
            SetStatus("Update korte parlam na: " + ex.Message, isError: true);
        }
    }

    public async Task DeleteAsync(ScheduleRow row)
    {
        try
        {
            await AppServices.Schedules.DeleteAsync(row.Id);

            if (AppServices.Protection is not null)
            {
                await AppServices.Protection.RefreshAsync(lockRunningInstances: false);
            }

            await AppServices.ActivityLog.LogAsync(
                ActivityEventType.Settings,
                ActivityOutcome.Success,
                $"Protection window '{row.Name}' deleted.");

            await LoadAsync();
            SetStatus($"{row.Name} delete kora holo.", isError: false);
            ScheduleChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Deleting schedule window failed");
            SetStatus("Delete korte parlam na: " + ex.Message, isError: true);
        }
    }

    private static string Shift(string time, int steps)
    {
        var minutes = ParseMinutes(time) ?? 9 * 60;
        var index = (int)Math.Round(minutes / 30.0);
        var count = 48;
        index = ((index + steps) % count + count) % count;

        return ProtectionSchedule.FormatMinutes(index * 30);
    }

    private static int? ParseMinutes(string time)
    {
        var parts = time.Split(':');

        return parts.Length == 2
            && int.TryParse(parts[0], out var hours)
            && int.TryParse(parts[1], out var minutes)
            && hours is >= 0 and < 24
            && minutes is >= 0 and < 60
                ? hours * 60 + minutes
                : null;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            var hours = (int)duration.TotalHours;
            var minutes = duration.Minutes;
            return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
        }

        return $"{Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes))} min";
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}
