# ChangeLog

## 2026-10-23 – English project documentation

- Translated the main README and the complete changelog, including historical entries, into English.
- Reviewed the remaining project documentation and license notices, which were already in English.
- Preserved commands, technical identifiers, validation results, and unresolved limitations. Existing German UI labels are quoted and explained in English; application behavior is unchanged.

## 2026-10-23 – OpenHAB SVGs and fixed F7 rollershutter icons

- Assigned the original Framework7 SVGs `arrowtriangle_up`, `stop`, and `arrowtriangle_down` to the rollershutter actions. Arrows are gray (#929292), and STOP is red (#ff4545), matching the web UI reference. Users arrange the three keys vertically as UP/STOP/DOWN; the position display is retained.
- F7 SVGs are embedded local resources and require neither an icon font nor a network connection. The original MIT license and attribution notices are included in the build output.
- Lamps, sockets, and dimmers prefer SVGs from the configured OpenHAB server through `/icon/<category>?format=svg&anyFormat=true&iconset=classic&state=<state>`. Server-defined colors and state variants are retained.
- Lamp ON/OFF values map to 100/0; dimmers pass their actual level, and sockets pass ON/OFF. Explicit lamp/socket actions use `light` or `socket`; automatic actions use the item category. Dimmers without a category use `light`.
- Added a nonblocking in-memory cache for up to 128 category/state combinations, with at most four concurrent downloads, a five-second request timeout, and a 256 KiB SVG limit. Identical requests are coalesced. Successful entries refresh on demand after five minutes; failed requests are retried no sooner than 30 seconds later. The synchronous rendering call does not perform a download.
- Icon arrival triggers a display notification. Local symbols remain available for unknown states, missing icons, invalid SVGs, or rendering errors. Cache activity and downloads belong to the server session and end on reconfiguration or unload.
- Extended integration tests to cover authenticated icon retrieval, state encoding, intermediate levels, cache hits, request coalescing, ON/OFF transitions, and failure fallback. All local tests passed.
- Release build succeeded without warnings or errors; all three F7 resources and the license files were verified in the output.
- The five URLs supplied by the user return HTTP 200 and valid, distinct SVG content through curl with certificate validation enabled. The optional direct .NET client live test on this Mac instead fails during TLS negotiation with `AppleCrypto: bad certificate format`, using both the installed .NET 8 and .NET 9 runtimes. The cause remains unresolved; certificate validation is not bypassed. Successful icon retrieval in the actual Logi host has therefore not been confirmed.
- Updated the README with vertical key arrangement, icon behavior, caching, and the optional icon-only live test using `OPENHAB_ICON_SERVER`. No commands were sent to real devices.

## 2026-10-23 – OpenHAB NG: live states and new controls

### Original defects

- WebSocket initialization was commented out. The receive loop exited before processing completed messages, the heartbeat timer was never started, authentication was unused, and fragmented messages were not reassembled.
- Events used item names while profiles stored absolute URLs. Switches blindly toggled their state instead of adopting the reported state; an unknown state could also trigger an incorrect transition.
- Polling only covered registered items, ran as an unsupervised async-void loop, and continued after unload. Label selection mixed different item sets and could fail because of missing groups.
- Setting a new URL still initialized the old URL and could remove the token from the configuration.

### Connection and state model

- Rebuilt the service with an explicit lifecycle and cancellable background tasks. Reconfiguration ends the previous session; unloading stops HTTP requests, polling, and WebSocket activity.
- Added an initial REST retrieval of all items followed by WebSocket connectivity through `/ws/events`, or `/ws` for older servers. OpenHAB sends all events by default when a connection is established; no additional subscribe message is required.
- REST uses a Bearer token, and WebSocket authentication uses the documented subprotocols. Tokens and complete event payloads are not written to diagnostic output.
- Added five-second heartbeats, receive timeouts, reconnection with a capped delay, reconciliation after connecting, and reassembly of text messages up to 1 MiB.
- `ItemStateEvent` and `ItemStateChangedEvent` update the confirmed-state cache and trigger display notifications. Malformed, unrelated, and command-only events do not change the display; duplicate states do not cause blind toggling.
- REST reconciliation runs every five seconds without WebSocket connectivity and every 30 seconds while connected. Stale REST responses cannot overwrite newer events; removed items are reported as unknown. Responses from a superseded session are discarded.
- New profile parameters use stable item names instead of server-specific URLs. REST paths are built from the configured server address. HTTP failures are checked; commands are not presented as physically confirmed states.
- Disabled cookie handling for the token-based REST API, allowing operation in environments where the domain name cannot be queried.

### Controls and rendering

- Added live switch widgets with confirmed ON/OFF states, automatic symbols based on the item category, and explicit lamp/socket actions. The local lamp icon includes rays when ON; ON is yellow, and OFF is gray. Key rendering does not make network requests.
- Dimmers display percentages for numeric and ON/OFF states, safely handle NULL/UNDEF, constrain values to 0–100, use culture-independent numbers, and accumulate rapid turns when feedback is delayed. Changes refresh both the key image and the adjustment value.
- Added three independently placeable UP/STOP/DOWN rollershutter widgets for the same item, showing the reported position and sending the OpenHAB commands UP/STOP/DOWN. No multi-cell profile layout is created automatically.
- Added live state displays for all items, grouped selection without duplicate Others groups, safe handling of missing labels, and a connection status widget.
- Displays subscribe to state events on load and unsubscribe on unload.
- Added server URL and API token actions; changing the URL preserves the token. Credentials can also be entered directly in the NG `config.json`.

### Separate NG identity

- Package name: `OpenHABNG`; display name: `OpenHAB NG`; assembly: `OpenHABPluginNG.dll`; namespace: `Loupedeck.OpenHABPluginNG`; a separate assembly GUID and version 0.2.0.
- The plugin name separates profiles and the host-managed data directory from the original OpenHAB plugin. The old configuration is neither imported nor modified automatically.
- Legacy OpenHABPlugin binaries are removed from the local build output to prevent accidental inclusion in an NG package.
- Enabled the Windows metadata path. Validation covered the macOS Release build, not Windows runtime behavior.

### Documentation and validation

- Updated the main README with complete macOS and Windows build instructions, the verified ATEM SDK path, offline restoration, output and installation layout, configuration, widget usage, and test commands.
- The .NET 8 Release build against the API from the ATEM build succeeded without compiler warnings or errors. The currently installed .NET 10 Logi API was not used as a reference.
- Added executable integration tests under `tests/` using a real local HTTP/WebSocket test server without requiring `PluginApi.dll`. Coverage includes authentication, snapshots, heartbeats, fragmented messages, events, UI notification parameters, duplicate/malformed events, switch/rollershutter commands, HTTP failures, REST/WebSocket races, percentage formatting, reconnection, the legacy endpoint, reconfiguration, polling fallback, and shutdown.
- No live test against the user's OpenHAB installation or native rendering test on actual Loupedeck/Logi hardware was performed. Notification tests confirm the data path to the action, not actual drawing by the host. Loading in the installed .NET 10 host still requires separate verification.
- Protocol references: https://www.openhab.org/docs/configuration/websocket.html and https://v50.openhab.org/docs/configuration/websocket.html.

## 2026-10-22 – Migration to .NET 8 following the ATEM project

### Implementation

- Used the modern ATEM plugin as the reference: SDK-style project format (`Microsoft.NET.Sdk`), `TargetFramework=net8.0`, and NuGet through `PackageReference`. The previous approach of retaining .NET Framework 4.8 was discarded.
- Removed legacy framework references and explicit C# source lists; the SDK discovers source files automatically. Newtonsoft.Json 13.0.3 is restored through PackageReference and copied to the output using `CopyLocalLockFileAssemblies=true`.
- Removed `packages.config` and the framework-specific `App.config` with its old binding redirects.
- `GenerateAssemblyInfo=false` preserves existing assembly attributes without duplicate generation. At this stage, the assembly name and namespace were retained. The embedded PNG remains included.
- Removed the unused `System.Runtime.Remoting.Messaging`, `System.Security.Policy`, and `Loupedeck.Devices.Loupedeck2Devices` imports so the source compiles under .NET 8.
- Set the shared intermediate output path early in `Directory.Build.props`. Output is placed under `bin/<Configuration>/mac` or `win`, with metadata alongside it.
- Default SDK paths point to LogiPluginService, following the ATEM project. `PluginApiDir` remains overridable, and the host API is not bundled.

### Build and remaining validation

- Offline package restoration from the existing Newtonsoft.Json 13.0.3 cache succeeded.
- Release build against the .NET 8 API in `/Users/tseiman/Downloads/LoupedeckAtemControlerPlugin/bin/Debug/mac` succeeded without compiler errors. Full compilation reported four existing nullability warnings (CS8622 and CS8604); an unchanged incremental build also succeeded.
- Output at this stage: `bin/Release/mac/OpenHABPlugin.dll`, Newtonsoft.Json, the dependency manifest, and copied metadata.
- Build command from the repository: `dotnet build OpenHABPlugin.sln --configuration Release -p:PluginApiDir=/Users/tseiman/Downloads/LoupedeckAtemControlerPlugin/bin/Debug/mac`. The offline check first restored packages using `dotnet restore OpenHABPlugin.sln --source /Users/tseiman/.nuget/packages/newtonsoft.json/13.0.3 --packages ./packages`, then built with `--no-restore`.
- The installed Logi host already provides a .NET 10 API, which was not used for the .NET 8 build. Successful compilation against the ATEM API does not replace host loading or OpenHAB functionality tests; those remain outstanding.

## 2026-10-22 – Discarded intermediate step: .NET Framework 4.8 build configuration

This approach was discarded after the user clarified the requirement. The .NET 8 migration following the ATEM project, documented above, supersedes it. This section records only the historical intermediate state.

### Corrections at that stage

- Retained .NET Framework 4.8. Changed `packages.config` from `net6.0` to `net48` and `App.config` from v4.7.2 to v4.8. The Newtonsoft.Json assembly under `lib/net45` remained compatible and unchanged.
- Changed `Directory.Build.props` from the conflicting 7.3 language version to `latestmajor`, matching the project.
- Removed duplicate and macOS-only output paths. Debug and Release output went to `bin/<Configuration>/mac` or `bin/<Configuration>/win`; Debug builds no longer wrote directly into the plugin installation. Base paths also worked when building the project directly without its solution.
- Removed IDE-specific Debug commands that terminated Loupedeck processes.

### Improvements adopted from the ATEM plugin

The comparison primarily covered `src/LoupedeckAtemControlerPlugin/LoupedeckAtemControlerPlugin.csproj` and `src/Directory.Build.props` in `/Users/tseiman/Downloads/LoupedeckAtemControlerPlugin`. The ATEM project was not modified.

- Made the SDK directory configurable through `PluginApiDir`, retaining the previous Loupedeck paths as defaults at that stage. Set `Private=False` so the host-provided `PluginApi.dll` was not copied into the plugin output.
- Added an early, clear error for a missing SDK file. An alternative compatible SDK directory could be supplied with `/p:PluginApiDir=/path/to/SDK`.
- Replaced shell/batch post-build calls with a cross-platform MSBuild copy target for metadata, including subdirectories. The old scripts remained unused in the repository.

### Build attempts and verification

- The initial Mono MSBuild attempt failed because PluginApi and the Newtonsoft.Json package were missing.
- Restored Newtonsoft.Json 13.0.3 from the existing local NuGet cache to `packages/Newtonsoft.Json.13.0.3`. Package files remained local under the existing Git ignore rule. Fresh checkouts still required NuGet restoration.
- A subsequent Release build was blocked by the missing compatible Loupedeck SDK at `/Applications/Loupedeck.app/Contents/MonoBundle/PluginApi.dll`. Mono supplied the .NET Framework 4.8 reference assemblies.
- The installed API at `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll` targets `.NETCoreApp,Version=v10.0`; the API in the ATEM build targets v8.0. Neither replaces a .NET Framework 4.8 API. Installed dotnet SDKs extended through version 9.0.201.
- XML configuration files parsed successfully. The metadata target was run separately, and the copied YAML file was verified byte-for-byte. No successful plugin build or runtime test occurred at that stage.
- Build command: `/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild OpenHABPlugin.sln /t:Build /p:Configuration=Release /verbosity:minimal /nologo` from the repository directory.

### Further modernization options identified at that stage

- The ATEM plugin used SDK-style projects, `PackageReference`, and net8.0. This was identified as a useful migration, subject to the target host version; the installed host already provided a .NET 10 API. No unverified net8.0 switch was made at that intermediate stage.
- Migrating to the current Logi host would require a matching .NET 10 SDK and review or adjustment of the plugin API usage and legacy framework namespaces.
- Automatic plugin-link installation from the ATEM project was not adopted because the target host had not been established. Web configuration and shared-state helpers were possible future features, not requirements for the build repair.

## 2026-10-22 – Import of manual changes

Source: `/Users/tseiman/Downloads/Loupedeck_OpenHABPlugin`.
Destination: `/Users/tseiman/Downloads/LoupedeckOpenHABPlugin-NG`.
The date records the import, not when the original changes were made.

### Imported changes

- `OpenHABPlugin/OpenHABPlugin.csproj`: raised .NET Framework from 4.7.2 to 4.8.
- Changed the C# language setting for Debug and Release from `8.0` to `latestmajor`; the major language version therefore follows the installed compiler.
- Added `externalConsole=True` to the Debug post-build command `killall LoupedeckConfig || true`.
- Updated the Newtonsoft.Json reference path from 13.0.2 to 13.0.3, still using the assembly under `lib/net45`.
- `OpenHABPlugin/packages.config`: updated Newtonsoft.Json from 13.0.2 to 13.0.3 and `targetFramework` from `net472` to `net6.0`, exactly as in the manually maintained version.

### Comparison and notes

- Both modified project files were copied byte-for-byte from the manual version and verified as valid XML.
- C# source and the remaining shared project files were identical; no additional functionality changes were found.
- Local IDE data (`.vs`), build output (`bin`, `obj`), `.DS_Store`, and downloaded NuGet packages (`packages`) were not imported.
- The destination project's Git metadata and remote were retained. At this stage, the rename affected the repository name; both versions still used the assembly `OpenHABPlugin` and namespace `Loupedeck.OpenHABPlugin`.
- The imported package value `targetFramework=net6.0` differed from the project's .NET Framework 4.8 target. It was retained for an exact import and did not constitute a migration to .NET 6.
- A full build and Loupedeck/OpenHAB functionality test were not performed during this import.
