# ChangeLog

## 2026-10-23 – Consistent icon geometry and compact dimmer displays

- Replaced variable one/two-line caption heights with a fixed caption row. Long captions are shortened without changing the icon's size or position for a given image size.
- Added a dedicated dimmer layout shared by rotary dimmers and dimmer ON/OFF buttons: abbreviated heading at the top, centered icon, and bottom-centered percentage. Headings retain the last four Unicode text elements, e.g. `Dimmer, West` becomes `…West`.
- Omitted dimmer headings on displays shorter than 50 pixels or narrower than 32 pixels. Percentage spacing and font size adapt to narrow displays. Removed the side-by-side dimmer layout that crowded the dial strips.
- Validation: Release build and real host SDK tests passed, including equal icon geometry for short/long captions, suffix abbreviation, and dimmer images at 90×90, 60×60, 160×40, 48×48, 40×80, 60×30, and 20×50 pixels. The host template, state-dependent colors, item selections, and dimmer commands remain covered.

## 2026-10-23 – Full-key icon layout and numeric brightness controls

- Added an image-only `metadata/DefaultIconTemplate.ict` with a 100% image area and no host caption. The template prevents Action Editor profile images from being reduced to make room for an additional user-entered action name. Existing `IsWidget` flags remain enabled.
- Changed the rendered background to black, increased caption/value font sizes, and enlarged complete icons within the space between text rows. The top item label is retained; the state row does not obscure the icon. Short, wide dial displays retain their separate layout.
- Included writable `Number` and `Number:Dimensionless` brightness controls tagged with both `Control` and `Brightness` in dimmer selection, including typed groups. REST metadata now retains semantic tags and read-only status. Native dimmers remain supported; numeric measurements and read-only values are excluded. Catalog notifications account for changes in dimmer eligibility.
- Applied percentage formatting and state-dependent icon normalization to the additional brightness controls. Added coverage for numeric dimmer commands, numeric groups, measurement exclusion, and read-only exclusion.
- Validated the packaged template with the actual installed host `ActionIcon` and `ActionIconBuilder`, not just the plugin bitmap renderer. Setting an extra caption named `test` creates no text item, and the composed image is pixel-identical to the original full-size image. Release build and local SDK/integration tests passed. No real devices were operated.
- Existing item selections remain compatible. The host must reload the updated metadata; profiles retaining a previous/custom icon layout may need their icon reset to the plugin default.

## 2026-10-23 – Repair empty selectors and add connection health colors

- Fixed a regression introduced by the Action Editor migration: the SDK normalizes `Group` and `Item` control names to `group` and `item`, but case-sensitive event comparisons returned no options for real host requests. Controls, parameter lookup, and change notifications now consistently use lowercase identifiers; event comparisons are case-insensitive.
- Extended SDK regression coverage to obtain actual exported control names for every item action before requesting group and item options. Earlier tests manually passed uppercase names and missed SDK normalization. Existing Action Editor assignments remain compatible.
- Added green connection icons for working WebSocket and REST item retrieval, orange for degraded operation or connecting, and red for no working connection. A live WebSocket no longer hides failed REST retrieval behind the LIVE caption. Stopping the service now clears the previous successful REST status.
- Validation: Release build, real SDK selector/rendering tests, and local REST/WebSocket integration tests passed. Tests cover all item action selectors, exact status icon colors, partial connectivity, REST fallback, and disconnected status.

## 2026-10-23 – Correct item selection and host rendering

- Replaced legacy tree profile actions with SDK Action Editor controls carrying explicit item parameters into rendering and execution. Internal profile IDs are no longer treated as OpenHAB item names. Existing item-action assignments must be removed and assigned again once; URL and token configuration remain intact.
- Added an optional group filter with an All groups entry. Item catalogs refresh the selectors independently of state/icon updates. Included commandable typed groups, such as `Group:Rollershutter`, in the corresponding item selections.
- Updated all visible action instances when confirmed states or cached icons change. Unknown items display a selection prompt rather than a raw identifier.
- Added a dedicated Dimmer ON/OFF (0/100%) touch action. It and the dimmer dial press send `100` for 0 %, or `0` for any confirmed value above 0 %. Unknown states do not trigger a guessed command.
- Added SDK Action Editor display names and responsive bitmap layouts for square keys and short, wide dial displays, including item labels and percentage values.
- Explicitly rasterized SVGs with the host SDK before drawing. This avoids the implicit monochrome recoloring that removed original OpenHAB/F7 colors. Cached up to 128 finished raster images by size, caption, value, symbol, active state, and SVG hash. Each host request receives a separate image object.
- Extended fallback handling to cover SVG rasterization failures. The red F7 STOP symbol and original server colors are preserved.
- Replaced the ambiguous REST / OFFLINE caption with LIVE, REST, connection progress, and distinct HTTP/TLS/timeout errors.
- Validation: local REST/WebSocket integration tests passed, including typed-group selection and explicit dimmer commands. Tests against the installed real SDK passed for selector options, item captions, shutter commands, dimmer button/dial behavior, 90×90/60×60/160×40 images, state-dependent output, cached rendering, red STOP, server SVG colors, and malformed-SVG fallback. No physical devices were operated.
- Remaining environment issue: a standalone .NET 10 HTTPS icon probe fails with `AppleCrypto: bad certificate format`, while curl retrieves the same icon with certificate verification enabled. Live icon retrieval and authenticated item discovery in the actual host cannot be claimed as verified. Certificate validation has not been bypassed. Online NuGet vulnerability metadata was also unavailable during validation; builds can use the local dependency cache.

## 2026-10-23 – Use the installed .NET 10 host API

- The plugin and integration tests now target `net10.0`, matching the `PluginApi.dll` shipped with the installed Logi Plugin Service. The installed API references System.Runtime 10.0 and cannot be compiled against the previous .NET 8 target (CS1705).
- The default reference is the DLL in the standard host installation directory. Removed automatic selection of the former local SDK copy; explicit PluginApiDir and OPENHAB_PLUGIN_SDK_DIR overrides remain available.
- Added global.json to select .NET SDK 10.0.100 or a newer .NET 10 feature band. A .NET 10 SDK is required once per development machine; its compiler and included runtimes are shared by the plugin, integration tests, and current tooling.
- Updated the main README with the installed-host workflow and .NET 10 prerequisites. No host DLL is copied into the repository or bundled in the plugin output.
- Release build against the installed host API succeeded with SDK 10.0.402, without warnings or errors. All local HTTP/WebSocket/icon integration checks passed under .NET 10. Hardware rendering and loading in the host remain separate runtime checks.
- Validation used a temporary SDK installation outside the repository. The existing system-wide .NET installation was not modified during validation.

## 2026-10-23 – Independent SDK discovery and development registration

- Added host SDK discovery through an explicit PluginApiDir, OPENHAB_PLUGIN_SDK_DIR, a Git-ignored local sdk/ directory, and the standard host installation path, in that order. A compatible local SDK now allows a normal build without path arguments.
- Kept the .NET 8 target and verified that the currently installed .NET 10 host API fails with CS1705. A .NET 8-compatible local SDK is used for successful validation; no host binaries are committed or bundled.
- Added optional InstallPluginLink registration using cross-platform MSBuild file operations. OpenHABNG.link points to the configuration root containing metadata and the platform folder. PluginDir remains configurable and paths with spaces are supported. Normal builds do not register or restart the host.
- Cleaning now removes copied metadata and, when registration is enabled, deletes a registration link only if it still points to the output being cleaned.
- Rewrote the build instructions as a standalone workflow and removed comparison-project references from all current project files, including historical changelog wording.
- Validation passed for SDK selection precedence, missing-SDK errors, optional registration in a path containing spaces, matching-link removal, preservation of links pointing elsewhere, metadata cleanup, and a final Release build without an explicit SDK argument. No registration was written to the actual host plugin directory.

## 2026-10-23 – Standalone OpenHAB build instructions

- Removed the unrelated plugin checkout example from the README. Building OpenHAB NG requires only its own sources, the .NET SDK, restored NuGet packages, and a compatible Loupedeck/Logi `PluginApi.dll`.
- Clarified that the dependency is the host SDK, not another plugin. Historical comparisons with the modernization reference remain recorded below; they are not build requirements.
- Replaced wording that incorrectly called the shared Loupedeck/Logi host library an API belonging to another plugin.

## 2026-10-23 – Portable documentation examples

- Removed developer-specific absolute paths and the private OpenHAB hostname from all project documentation, including historical changelog entries.
- Build and test examples now use a checkout-relative directory, a compatible SDK placeholder, a generic offline NuGet feed. The live icon test uses an example server address.
- Historical build commands are shown with portable path substitutions. Public repository URLs, standard host installation paths, and license attribution remain intact.

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

- Updated the main README with complete macOS and Windows build instructions, the verified host SDK path, offline restoration, output and installation layout, configuration, widget usage, and test commands.
- The .NET 8 Release build against a compatible host SDK succeeded without compiler warnings or errors. The currently installed .NET 10 Logi API was not used as a reference.
- Added executable integration tests under `tests/` using a real local HTTP/WebSocket test server without requiring `PluginApi.dll`. Coverage includes authentication, snapshots, heartbeats, fragmented messages, events, UI notification parameters, duplicate/malformed events, switch/rollershutter commands, HTTP failures, REST/WebSocket races, percentage formatting, reconnection, the legacy endpoint, reconfiguration, polling fallback, and shutdown.
- No live test against the user's OpenHAB installation or native rendering test on actual Loupedeck/Logi hardware was performed. Notification tests confirm the data path to the action, not actual drawing by the host. Loading in the installed .NET 10 host still requires separate verification.
- Protocol references: https://www.openhab.org/docs/configuration/websocket.html and https://v50.openhab.org/docs/configuration/websocket.html.

## 2026-10-22 – Migration to .NET 8

### Implementation

- Adopted a modern build configuration: SDK-style project format (`Microsoft.NET.Sdk`), `TargetFramework=net8.0`, and NuGet through `PackageReference`. The previous approach of retaining .NET Framework 4.8 was discarded.
- Removed legacy framework references and explicit C# source lists; the SDK discovers source files automatically. Newtonsoft.Json 13.0.3 is restored through PackageReference and copied to the output using `CopyLocalLockFileAssemblies=true`.
- Removed `packages.config` and the framework-specific `App.config` with its old binding redirects.
- `GenerateAssemblyInfo=false` preserves existing assembly attributes without duplicate generation. At this stage, the assembly name and namespace were retained. The embedded PNG remains included.
- Removed the unused `System.Runtime.Remoting.Messaging`, `System.Security.Policy`, and `Loupedeck.Devices.Loupedeck2Devices` imports so the source compiles under .NET 8.
- Set the shared intermediate output path early in `Directory.Build.props`. Output is placed under `bin/<Configuration>/mac` or `win`, with metadata alongside it.
- Default SDK paths point to LogiPluginService. `PluginApiDir` remains overridable, and the host API is not bundled.

### Build and remaining validation

- Offline package restoration from the existing Newtonsoft.Json 13.0.3 cache succeeded.
- Release build against the .NET 8 API in the locally available host SDK succeeded without compiler errors. Full compilation reported four existing nullability warnings (CS8622 and CS8604); an unchanged incremental build also succeeded.
- Output at this stage: `bin/Release/mac/OpenHABPlugin.dll`, Newtonsoft.Json, the dependency manifest, and copied metadata.
- Build command from the repository: `dotnet build OpenHABPlugin.sln --configuration Release -p:PluginApiDir="/path/to/compatible/SDK"`. The offline check first restored packages using `dotnet restore OpenHABPlugin.sln --source "/path/to/offline-nuget-feed" --packages ./packages`, then built with `--no-restore`.
- The installed Logi host already provides a .NET 10 API, which was not used for the .NET 8 build. Successful compilation against the compatible host API does not replace host loading or OpenHAB functionality tests; those remain outstanding.

## 2026-10-22 – Discarded intermediate step: .NET Framework 4.8 build configuration

This approach was discarded after the user clarified the requirement. The .NET 8 migration documented above supersedes it. This section records only the historical intermediate state.

### Corrections at that stage

- Retained .NET Framework 4.8. Changed `packages.config` from `net6.0` to `net48` and `App.config` from v4.7.2 to v4.8. The Newtonsoft.Json assembly under `lib/net45` remained compatible and unchanged.
- Changed `Directory.Build.props` from the conflicting 7.3 language version to `latestmajor`, matching the project.
- Removed duplicate and macOS-only output paths. Debug and Release output went to `bin/<Configuration>/mac` or `bin/<Configuration>/win`; Debug builds no longer wrote directly into the plugin installation. Base paths also worked when building the project directly without its solution.
- Removed IDE-specific Debug commands that terminated Loupedeck processes.

### Build improvements

Build changes covered the project configuration and shared MSBuild properties.

- Made the SDK directory configurable through `PluginApiDir`, retaining the previous Loupedeck paths as defaults at that stage. Set `Private=False` so the host-provided `PluginApi.dll` was not copied into the plugin output.
- Added an early, clear error for a missing SDK file. An alternative compatible SDK directory could be supplied with `/p:PluginApiDir=/path/to/SDK`.
- Replaced shell/batch post-build calls with a cross-platform MSBuild copy target for metadata, including subdirectories. The old scripts remained unused in the repository.

### Build attempts and verification

- The initial Mono MSBuild attempt failed because PluginApi and the Newtonsoft.Json package were missing.
- Restored Newtonsoft.Json 13.0.3 from the existing local NuGet cache to `packages/Newtonsoft.Json.13.0.3`. Package files remained local under the existing Git ignore rule. Fresh checkouts still required NuGet restoration.
- A subsequent Release build was blocked by the missing compatible Loupedeck SDK at `/Applications/Loupedeck.app/Contents/MonoBundle/PluginApi.dll`. Mono supplied the .NET Framework 4.8 reference assemblies.
- The installed API at `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll` targets `.NETCoreApp,Version=v10.0`; the separately available compatible host API targets v8.0. Neither replaces a .NET Framework 4.8 API. Installed dotnet SDKs extended through version 9.0.201.
- XML configuration files parsed successfully. The metadata target was run separately, and the copied YAML file was verified byte-for-byte. No successful plugin build or runtime test occurred at that stage.
- Build command: `/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild OpenHABPlugin.sln /t:Build /p:Configuration=Release /verbosity:minimal /nologo` from the repository directory.

### Further modernization options identified at that stage

- SDK-style projects, `PackageReference`, and net8.0 were considered for modernization. This was identified as a useful migration, subject to the target host version; the installed host already provided a .NET 10 API. No unverified net8.0 switch was made at that intermediate stage.
- Migrating to the current Logi host would require a matching .NET 10 SDK and review or adjustment of the plugin API usage and legacy framework namespaces.
- Automatic plugin-link installation was not adopted because the target host had not been established. Web configuration and shared-state helpers were possible future features, not requirements for the build repair.

## 2026-10-22 – Import of manual changes

Source: the manually maintained `Loupedeck_OpenHABPlugin` directory.
Destination: the `LoupedeckOpenHABPlugin-NG` checkout.
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
