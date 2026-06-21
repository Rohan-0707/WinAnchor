namespace InScreenApp.Models;

public sealed class WindowInfo
{
    public IntPtr Handle { get; init; }

    public required string Title { get; init; }

    public required string ProcessName { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(Title)
        ? $"(Untitled) — {ProcessName}"
        : $"{Title} — {ProcessName}";
}
