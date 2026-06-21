# WinAnchor

Pin and anchor Windows application windows in-screen — compact, always-on-top views you can move and resize from a simple control panel.

## Features

- Pin multiple windows at once
- Pick windows from a list or use **Ctrl + Alt + T** on the active window
- Shrinks fullscreen/maximized windows to an in-screen mini view
- Direction pad to reposition pinned windows
- Smaller / Larger resize controls
- Unpin selected windows and restore them to full screen
- Windows installer for easy setup

## Download

Get the latest installer from [GitHub Releases](https://github.com/Rohan-0707/WinAnchor/releases).

## Requirements

- Windows 10/11 (64-bit)

## Build from source

```powershell
dotnet publish InScreenApp.csproj -c Release -o publish\win-x64
```

Build the installer (requires [Inno Setup](https://jrsoftware.org/isinfo.php)):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1
```

## Run during development

```powershell
dotnet run -c Release
```

## Developer

**Rohan Kumar Bhoi** · Bangalore, India

Head of IT & Digital Marketing @ Wzatco Projectors (India’s No.1 Projector Brand) | Director, Bhoi Enterprises | Director & CEO, Lorvantiq Ventures PVT. LTD. | Full Stack Software Engineer | Digital Growth Specialist

| | |
|---|---|
| **Email** | [me.rohanbhoi@gmail.com](mailto:me.rohanbhoi@gmail.com) |
| **Portfolio** | [rohanbhoi.in](https://rohanbhoi.in) |
| **LinkedIn** | [linkedin.com/in/ceorohan](https://linkedin.com/in/ceorohan) |
| **Instagram** | [instagram.com/ceorohan](https://instagram.com/ceorohan) |
| **X / Twitter** | [x.com/ceorohan1](https://x.com/ceorohan1) |

## License

MIT
