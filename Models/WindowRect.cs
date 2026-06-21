namespace InScreenApp.Models;

public readonly struct WindowRect
{
    public int Left { get; init; }
    public int Top { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    public int Right => Left + Width;
    public int Bottom => Top + Height;
}
