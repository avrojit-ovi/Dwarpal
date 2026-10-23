using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dwarpal.App.Services;
using Dwarpal.Core.Models;
using Serilog;

namespace Dwarpal.App.ViewModels;

public sealed record ActivityLogRow(
    long Id,
    string DateText,
    string TimeText,
    string EventLabel,
    string OutcomeLabel,
    string? AppName,
    string Detail,
    bool HasSelfie,
    string? SelfiePath,
    bool IsFailure)
{
    public string Title => $"{EventLabel} · {OutcomeLabel}";
}

public partial class ActivityViewModel : ObservableObject
{
    private const int MaxRows = 1000;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasRows;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _count;

    public bool IsEmpty => !HasRows;

    public ObservableCollection<ActivityLogRow> Rows { get; } = new();

    public async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            var entries = await AppServices.Activity.QueryAsync(
                search: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                limit: MaxRows);

            Rows.Clear();

            foreach (var entry in entries)
            {
                var local = entry.TimestampUtc.ToLocalTime();

                Rows.Add(new ActivityLogRow(
                    entry.Id,
                    local.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                    local.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    EventLabel(entry.EventType),
                    OutcomeLabel(entry.Outcome),
                    entry.AppName,
                    entry.Detail ?? string.Empty,
                    !string.IsNullOrWhiteSpace(entry.SelfiePath),
                    entry.SelfiePath,
                    entry.Outcome == ActivityOutcome.Failure));
            }

            Count = Rows.Count;
            HasRows = Count > 0;
            StatusMessage = Count == 0
                ? "Kono activity nei."
                : $"{Count} event(s) · newest first (max {MaxRows})";
            IsStatusError = false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Activity log load failed");
            StatusMessage = "Activity load korte parlam na: " + ex.Message;
            IsStatusError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    public async Task ExportCsvAsync(string path)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("Timestamp (local),Event,Outcome,App,Detail,Selfie");

            foreach (var row in Rows)
            {
                builder.AppendLine(string.Join(
                    ',',
                    Escape($"{row.DateText} {row.TimeText}"),
                    Escape(row.EventLabel),
                    Escape(row.OutcomeLabel),
                    Escape(row.AppName),
                    Escape(row.Detail),
                    Escape(row.HasSelfie ? "Yes" : "No")));
            }

            await File.WriteAllTextAsync(
                path,
                builder.ToString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            StatusMessage = $"Exported {Rows.Count} event(s) to {Path.GetFileName(path)}.";
            IsStatusError = false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Activity CSV export failed");
            StatusMessage = "CSV export korte parlam na: " + ex.Message;
            IsStatusError = true;
        }
    }

    private static string EventLabel(ActivityEventType eventType) => eventType switch
    {
        ActivityEventType.Setup => "Setup",
        ActivityEventType.Unlock => "Unlock",
        ActivityEventType.Lock => "App lock",
        ActivityEventType.Recovery => "Recovery",
        ActivityEventType.Settings => "Settings",
        ActivityEventType.Tamper => "Tamper",
        ActivityEventType.Error => "Error",
        _ => eventType.ToString(),
    };

    private static string OutcomeLabel(ActivityOutcome outcome) => outcome switch
    {
        ActivityOutcome.Success => "success",
        ActivityOutcome.Failure => "failed",
        _ => "info",
    };

    /// <summary>CSV injection thekano: =, +, -, @ diye shuru hole Excel formula hisebe na chole.</summary>
    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;

        if (text.Length > 0 && "=+-@".Contains(text[0]))
        {
            text = "'" + text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
