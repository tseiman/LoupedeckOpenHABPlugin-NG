# Loupedeck OpenHAB Plugin NG

OpenHAB NG connects Loupedeck CT, Live, Live S, and Razer Stream Controller to OpenHAB. The NG version uses .NET 10 and has its own identity, allowing it to be configured alongside the original OpenHAB plugin.

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

- .NET 10 SDK. Integration tests also require the .NET 10 and ASP.NET Core 10 runtimes, which are included in the SDK. Check installed versions with `dotnet --list-sdks` and `dotnet --list-runtimes`.
- Logi Plugin Service installed by the host application, providing its .NET 10 `PluginApi.dll`.
- NuGet access for Newtonsoft.Json 13.0.3, or a local package source.

The project is self-contained apart from its host SDK and NuGet dependencies. No other plugin checkout is required.

### Host API

By default, the project references `PluginApi.dll` directly from the installed Logi Plugin Service:

- macOS: `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll`
- Windows: `C:/Program Files/Logi/LogiPluginService/PluginApi.dll`

No DLL needs to be copied into the repository. The host API is not downloaded by package restore and is not bundled in the plugin output. The former local `sdk/` directory is no longer selected automatically, so an old API copy cannot silently override the installed host.

If the host is installed elsewhere, use `-p:PluginApiDir="/path/to/sdk"` or set `OPENHAB_PLUGIN_SDK_DIR` to the directory containing the DLL. The command-line property takes precedence over the environment variable. The build prints the selected directory.

Both the plugin and its integration tests target `net10.0`. `global.json` selects a .NET 10 SDK and permits newer .NET 10 feature bands. Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) if `dotnet --list-sdks` does not show a 10.0 SDK. Installing only the runtime is insufficient for compilation.

The optional `LogiPluginTool` utility creates project templates and packages; it is not required to build this existing solution. Its installation does not replace the host API reference.

### Build

From the directory containing your checkout:

```bash
cd LoupedeckOpenHABPlugin-NG
```

With the .NET 10 SDK and host application installed, build with automatic package restoration:

```bash
dotnet build OpenHABPlugin.sln --configuration Release
```

The same command works on macOS and Windows. Debug builds use `--configuration Debug`. To select an SDK explicitly:

```bash
dotnet build OpenHABPlugin.sln --configuration Release -p:PluginApiDir="/path/to/sdk"
```

On Windows, use a Windows SDK path, for example `-p:PluginApiDir="C:/path/to/sdk"`.

### Offline build

Use a local NuGet feed containing the Newtonsoft.Json 13.0.3 package (`.nupkg`). Replace the example directory with your package source:

```bash
dotnet restore OpenHABPlugin.sln --source "/path/to/offline-nuget-feed" --packages ./packages
```

Then build without restoring packages again:

```bash
dotnet build OpenHABPlugin.sln --configuration Release --no-restore
```

### Output

```text
bin/Release/
  mac/                         # win/ on Windows
    OpenHABPluginNG.dll
    OpenHABPluginNG.deps.json
    Newtonsoft.Json.dll
    licenses/
  metadata/
    LoupedeckPackage.yaml
    Icon*.png
```

Debug output is placed under `bin/Debug/`. The host API is not copied to the output. Legacy `OpenHABPlugin.dll` files are removed from the build output so they cannot accidentally be bundled with NG.

A successful .NET 10 build against the installed host API does not by itself verify loading or native rendering on actual hardware; those still require a host test.

### Optional local registration

A normal build only produces files. To also create the `OpenHABNG.link` development registration, explicitly enable it:

```bash
dotnet build OpenHABPlugin.sln --configuration Release -p:InstallPluginLink=true
```

The link points to the absolute `bin/Release` directory containing `metadata` and the platform folder. The default plugin directory is:

- macOS: `~/Library/Application Support/Logi/LogiPluginService/Plugins/`
- Windows: `%LOCALAPPDATA%/Logi/LogiPluginService/Plugins/`

Override it with `-p:PluginDir="/path/to/host/Plugins"` if necessary. Paths containing spaces are supported. Registration does not restart the host.

### Clean

```bash
dotnet clean OpenHABPlugin.sln --configuration Release
```

Cleaning removes build outputs and copied metadata. If registration was enabled, use the same registration options to remove its link as well:

```bash
dotnet clean OpenHABPlugin.sln --configuration Release -p:InstallPluginLink=true
```

If you supplied a custom `PluginDir`, pass it to both build and clean. The link is deleted only when its contents still point to the configuration being cleaned; a link belonging to another output directory is retained.

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
