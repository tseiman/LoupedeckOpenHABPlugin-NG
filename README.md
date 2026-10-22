# Loupedeck OpenHAB Plugin NG

OpenHAB NG connects Loupedeck CT, Live, Live S, and Razer Stream Controller to OpenHAB. The NG version uses .NET 8 and has its own identity, allowing it to be configured alongside the original OpenHAB plugin.

## Features

- Initial REST retrieval of all items, live updates through WebSocket events, and automatic reconnection after connection loss.
- WebSocket endpoints `/ws/events` and, as a fallback for older OpenHAB versions, `/ws`; a heartbeat is sent every five seconds.
- REST reconciliation every five seconds without WebSocket connectivity, or every 30 seconds while connected, to recover missed events.
- Switches with state-dependent icons; automatic lamp/socket selection based on the OpenHAB icon category, plus explicit **Lamp ON/OFF** and **Socket ON/OFF** actions.
- Dimmers with percentage displays, rotary control from 0 to 100, and push-to-toggle operation.
- Rollershutter control using three vertically arranged widgets: **UP**, **STOP**, and **DOWN**. Each displays the reported position and sends its command to the same selected `Rollershutter` item. The current action labels are **Rollladen AUF**, **Rollladen STOP**, and **Rollladen AB**.
- Live state displays for any item and a WebSocket connection status widget.

## Build instructions

### Prerequisites

1. .NET SDK 8 or later with the .NET 8 targeting packs. Integration tests also require the .NET 8 and ASP.NET Core 8 runtimes. Check installed SDKs with `dotnet --list-sdks` and runtimes with `dotnet --list-runtimes`.
2. A **.NET 8-compatible `PluginApi.dll`** from the Loupedeck/Logi SDK or a corresponding host installation.
3. NuGet access for Newtonsoft.Json 13.0.3, or an existing local package cache.

The LogiPluginService installation on the development Mac already provides a **.NET 10 API**. That DLL is not a suitable reference for this `net8.0` project. The API from the existing local ATEM build is the verified .NET 8 reference. This does not modify the ATEM repository or make it a hard-coded project dependency.

### macOS: verified local build

From the directory containing your checkout, change to the repository directory:

```bash
cd LoupedeckOpenHABPlugin-NG
```

Build with automatic package restoration:

```bash
dotnet build OpenHABPlugin.sln --configuration Release -p:PluginApiDir="/path/to/compatible/SDK"
```

If a compatible API is available in an ATEM checkout next to this repository, its relative path can also be used:

```bash
dotnet build OpenHABPlugin.sln --configuration Release -p:PluginApiDir="../LoupedeckAtemControlerPlugin/bin/Debug/mac"
```

`PluginApiDir` is the directory containing `PluginApi.dll`. The default is `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/` on macOS and `C:/Program Files/Logi/LogiPluginService/` on Windows. If that installation contains a newer API, explicitly provide a compatible path. The plugin continues to target .NET 8, following the ATEM project.

### Offline build using a local package source

Use a local NuGet feed containing the Newtonsoft.Json 13.0.3 package (`.nupkg`). Replace the example source directory with the location of your feed:

```bash
dotnet restore OpenHABPlugin.sln --source "/path/to/offline-nuget-feed" --packages ./packages
```

Then build without restoring packages again:

```bash
dotnet build OpenHABPlugin.sln --configuration Release --no-restore -p:PluginApiDir="/path/to/compatible/SDK"
```

### Windows

Run this command from the repository directory with a compatible SDK path:

```powershell
dotnet build OpenHABPlugin.sln --configuration Release -p:PluginApiDir="C:/path/to/compatible/SDK"
```

### Output and installation

The build creates this directory structure:

```text
bin/Release/
  mac/                         # win/ on Windows
    OpenHABPluginNG.dll
    OpenHABPluginNG.deps.json
    Newtonsoft.Json.dll
  metadata/
    LoupedeckPackage.yaml
    Icon*.png
```

The host API is not bundled. Debug output is placed under `bin/Debug/`. Building does not automatically install the plugin or restart the host. Load the package under its new name, **OpenHABNG**. For a local development installation, the plugin link must point to the directory containing `mac` or `win` and `metadata`, for example `bin/Release`.

Compilation against the ATEM API has been verified locally. Loading in the currently installed .NET 10 host and rendering on actual hardware still require separate testing; a successful build does not establish that compatibility. The build removes legacy `OpenHABPlugin.dll` output from its local target directory; those files must not be included in an NG package.

## Configuring OpenHAB

1. Select **OpenHAB NG** in the host application.
2. Assign **Set OpenHAB URL** to a key, enter the server URL, such as `http://openhab.local:8080`, and run it once. Base paths behind reverse proxies are supported.
3. If required, run **Set OpenHAB API token**. The token is used for REST and WebSocket authentication. It is stored as a profile parameter and in the local `config.json`; handle that configuration accordingly. Alternatively, use `mcedit` to edit the token directly in the NG configuration directory without placing it in a key parameter.
4. Select the desired actions after the initial item retrieval. If an already-open selection menu is still empty, close and reopen it.

NG uses the data directory provided by the host for **OpenHABNG**. The old plugin and its configuration are not migrated automatically. A separate `config.json` can contain:

```json
{
  "url": "http://openhab.local:8080",
  "token": ""
}
```

Changing the URL preserves the token. Reconfiguration ends the previous connection. Tokens are not written to diagnostic output.

## Actions and displays

**Switch (automatic icon)** uses the item category: `light`, `lamp`, and `bulb` select the lamp symbol; categories containing `socket` or `outlet` select the socket symbol. Other categories use a generic switch. **Lamp ON/OFF** and **Socket ON/OFF** override that choice independently of the item category. Icons are preferably retrieved directly from OpenHAB through `/icon/<category>?format=svg&anyFormat=true&state=<state>&iconset=classic`, retaining the server-defined colors. For lights, OFF maps to 0 and ON to 100; dimmers use their actual percentage, while sockets use ON/OFF. Local yellow/gray icons serve as fallbacks when the category is missing, the state is unknown, or retrieval fails. Unknown values display as `—`. The reported server state is authoritative; pressing a key does not simulate a confirmed device state. Existing switch captions remain `AN`/`AUS` (ON/OFF).

**Dimmer (%)** displays numeric values as percentages, `ON` as `100 %`, `OFF` as `0 %`, and `NULL`/`UNDEF` as `—`. The OpenHAB icon also follows the percentage, for example `state=36`. Rotary adjustments are limited to 0–100. Rapid turns accumulate even when device feedback is delayed.

For **rollershutters**, arrange three keys vertically: **Rollladen AUF** (UP), **Rollladen STOP** (STOP), and **Rollladen AB** (DOWN), selecting the same item on each key. These are three independently placeable widgets; no predefined three-cell profile layout is created automatically. The symbols are embedded original Framework7 SVGs: UP uses the gray `arrowtriangle_up`, STOP the red `stop` square, and DOWN the gray `arrowtriangle_down`, matching the web UI reference. No network request or installed F7 font is required. Users arrange the keys vertically in their profiles. In OpenHAB, 0 % means fully open and 100 % fully closed.

**Live state** displays the current item value. OpenHAB-specific Java/printf patterns are not evaluated as C# format strings; units remain part of the raw value.

**Connection status** displays `LIVE` when the WebSocket connection is established, otherwise `REST / OFFLINE`. This status alone does not confirm that individual devices are reachable. Connection and HTTP problems appear in the host log with the `[OpenHAB NG]` prefix.

## Icon retrieval and caching

Key rendering does not wait for network requests. SVGs are loaded in the background and trigger a redraw when available. Identical category/state combinations share a request. The in-memory cache holds up to 128 variants, allows at most four concurrent downloads, and refreshes successful entries on demand after five minutes. Failed requests are retried no sooner than 30 seconds later. Cache activity and downloads stop when the URL changes or the plugin unloads. The configured API token is also used for icon requests.

Requests use the configured OpenHAB server; no private hostname is hard-coded in the implementation.

## Integration tests

The tests use a local OpenHAB test server on a randomly assigned loopback port. They do not operate real devices and do not require `PluginApi.dll`. Run from the repository directory:

```bash
dotnet run --project tests/OpenHAB.IntegrationTests.csproj
```

For an offline run, use the same local package source as above:

```bash
dotnet restore tests/OpenHAB.IntegrationTests.csproj --source "/path/to/offline-nuget-feed" --packages ./packages
```

```bash
dotnet run --project tests/OpenHAB.IntegrationTests.csproj --no-restore
```

Coverage includes REST/WebSocket authentication, initial states, fragmented events, heartbeats, state changes and display notifications, protection against stale REST responses, UP/STOP/DOWN commands, HTTP failures, percentage values, reconnection, the legacy endpoint, REST fallback, and shutdown of background polling. These tests do not replace verification of actual rendering in the Loupedeck/Logi host.

An optional live test retrieves SVGs only, without item queries or device commands on the specified server. Replace the example hostname and port with your OpenHAB server address:

```bash
OPENHAB_ICON_SERVER=https://openhab.example.org:8443 dotnet run --project tests/OpenHAB.IntegrationTests.csproj --no-restore -- --icons-only
```

On the development Mac, direct HTTPS requests through curl return valid SVGs, while the installed .NET 8 and .NET 9 runtimes fail with `AppleCrypto: bad certificate format`. Certificate validation remains enabled. The cause and behavior in the actual Logi host still require investigation; until then, only local fallback icons may be available there.

## Identity, origin, and licensing

Plugin ID: `OpenHABNG`; display name: `OpenHAB NG`; assembly: `OpenHABPluginNG.dll`; namespace: `Loupedeck.OpenHABPluginNG`.

This project is based on [Loupedeck_OpenHABPlugin by Arno Pernozzoli](https://github.com/pernozzoli/Loupedeck_OpenHABPlugin). Original license and copyright notices are retained. Changes and validation results are documented in [ChangeLog.md](ChangeLog.md). License: MIT; see [LICENSE](LICENSE).

The F7 symbols come from [Framework7 Icons](https://github.com/framework7io/framework7-icons). The MIT license and attribution files under `licenses/` are copied to the build output.

Protocol references: [OpenHAB WebSocket API](https://www.openhab.org/docs/configuration/websocket.html), [OpenHAB 5.0 WebSocket API](https://v50.openhab.org/docs/configuration/websocket.html), and [item types](https://www.openhab.org/docs/configuration/items.html).
