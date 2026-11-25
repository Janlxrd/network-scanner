# Network Scanner

A small Windows app that scans your home network and shows what's on it. It lists every device it finds with its IP, MAC address and vendor, guesses what kind of device it is, and draws a map of how everything connects to your router and the internet.

It scans when you click Scan. Nothing runs in the background.

## What it does

- Finds devices on your local IPv4 network with ARP and ping, including phones and PCs that ignore ping
- Looks up device names from your router's DNS, mDNS (Apple devices, Raspberry Pi OS) and NetBIOS (Windows PCs)
- Shows the vendor from the MAC address, using a copy of the IEEE vendor list that ships with the app
- Guesses the device type (phone, computer, TV, printer, Raspberry Pi and so on) from the name and vendor
- Lets you rename a device and set its type yourself
- Remembers devices between scans, so you can see what's offline and when it was last seen
- Shows routers between you and the internet, for example an ISP modem in front of your own router
- Dark and light themes

## Install

Download [NetworkScanner-Setup.exe](../../releases/latest/download/NetworkScanner-Setup.exe) from the latest release and run it. Older versions are on the [Releases](../../releases) page. It installs everything it needs, including the .NET runtime, so you don't have to install anything first.

The installer isn't code-signed yet, so Windows SmartScreen will warn you the first time. Click "More info", then "Run anyway".

Requires Windows 10 or 11 (64-bit). The app itself runs without admin rights.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). To build the installer you also need [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
winget install Microsoft.DotNet.SDK.10
winget install JRSoftware.InnoSetup
```

Run the app:

```powershell
dotnet run --project src/NetworkScanner
```

Run the tests:

```powershell
dotnet test
```

Build the installer. This runs the tests, publishes a self-contained build and writes `dist/NetworkScanner-Setup.exe`:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

`build.ps1` takes an optional version, for example `-Version 1.2.0`. Without it the version is 1.0.0.

To refresh the vendor list from the IEEE registry:

```powershell
powershell -ExecutionPolicy Bypass -File tools/update-oui.ps1
```

## Releases

Releases are built by GitHub Actions (`.github/workflows/release.yml`). Push a tag that starts with `v` and the workflow builds the installer on Windows, runs the tests and publishes a release with `NetworkScanner-Setup.exe` attached:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

The version shown in the app and the installer comes from the tag.

## How it works

1. Picks the network adapter Windows uses for internet traffic and skips VPN adapters.
2. Sends an ARP request and a ping to every address in the subnet at the same time. A device counts as online if it answers either one.
3. Devices it has seen before get one more try, because phones often sleep through the first round.
4. Asks each online device for its name.
5. Pings `1.1.1.1` with TTLs 1 to 4 to find the routers above yours.

A full scan of a normal home network takes about 10 seconds.

## Privacy

Everything stays on your PC. Vendor lookups use the bundled list, not an online service. The only traffic that leaves your network is the ping to `1.1.1.1` used to find upstream routers.

Saved devices and settings are stored in `%LOCALAPPDATA%\NetworkScanner`. When you uninstall, the uninstaller asks whether to delete them.

## Limits

- IPv4 only.
- Subnets larger than /22 are cut down to the /24 around your PC.
- The map is a star around your router. A normal scan can't see which device sits behind which switch or mesh point.
- Phones that use a private (random) MAC address show "Private MAC" instead of a vendor. You can rename them.

## Project layout

```
src/NetworkScanner.Core    scanning, vendor lookup, device types, saved devices
src/NetworkScanner         the WPF app
tests/NetworkScanner.Tests unit tests
installer                  Inno Setup script
tools                      vendor list updater
```

## License

Copyright (c) 2026 janiqwa. All rights reserved. You can read the code and use the official release for personal use, but you can't copy, change, reuse or redistribute it without permission. See [LICENSE](LICENSE).
