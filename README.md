# Loupedeck OpenHABPlugin

## What it is

This plugin provides you with labels, actions, and adjustments for your Loupedeck device to interact with your home automation system via [OpenHAB](https://www.openhab.org/).
This plugin requires a running OpenHAB server, reachable from the device, your Loupedeck device is attached to.

- [x] Works for MacOS (precompiled, work in progress)
- [x] Works for Windows (must be compiled from source)

## Release / Getting started

You can find the precompiled plugin (lplug4 file) for MacOS in the [Releases](https://github.com/pernozzoli/Loupedeck_OpenHABPlugin/releases) section. To install, download the plugin and open it. Loupedeck should recognize the file and install the plugin. Alternatively you may install the plugin from file within the Loupedeck configuration app.

To configure the URL of your OpenHAB server, you may add the action "Set OpenHAB URL" to your Loupedeck device, configure the URL in the corresponding action profile parameter and execute the action once on your Loupedeck device. You may remove the action once you've done this.

Alternatively you can configure your OpenHAB URL under the "url" property in the config.json file, that you can find in the corresponding Plugin data folder, e.g. `/Users/[Username]/.local/share/Loupedeck/PluginData/OpenHAB`.

You may have to restart the Loupedeck app after configuring the URL in order to see the OpenHAB items in the parameter section of each and every action.

## Usage

Upon start, the plugin reads out all available OpenHAB items to present them under their **first** corresponding group found. You can add switches and labels to regular button actions, dimmer can be added to the corresponding adjustment knobs. When configuring an action, you may select the group within the first combo-box in the settings, the second combo-box offers a list of items belonging to the selected group.

You may change the icons to visually represent the current state of the item. The state is updated around every 5 seconds. Also after device initialization the states are updated automatically.

## Restrictions

Labels are restricted to show the raw values only as the formatting provided from OpenHAB is based on C/C++/Java string formatting and the Loupedeck plugin is implemented in C#, furthermore, OpenHAB doesn't currently offer an API to provide an already converted displayable state including the formatting, e.g. for units.

Adjustment may have a slight delay, depending on your network connection and the reaction time of OpenHAB. Consider this when adjusting a dimmer.

When the OpenHAB URL changes you may have to restart the Loupedeck application. You shouldn't have to redefine individual actions, when item names and groups are not changed, but you will have to reassign an item when a name or a group assignment has changed.

## Support

If you'd like to drop me a star for the hours I've spent on this.


## Building the NG version

The NG project follows the ATEM plugin build configuration: .NET 8, SDK-style project, and PackageReference. Install a .NET 8 or newer SDK and provide a compatible .NET 8 PluginApi.dll. Build with `dotnet build OpenHABPlugin.sln --configuration Release -p:PluginApiDir=/path/to/sdk` from the repository root. NuGet restore is automatic.

By default, PluginApiDir points to LogiPluginService on macOS and Windows. If that installation contains an API targeting a newer framework (the API currently installed on this Mac targets .NET 10), select a compatible API directory explicitly. The verified local build used `/Users/tseiman/Downloads/LoupedeckAtemControlerPlugin/bin/Debug/mac`. This path is a local build input, not a repository dependency or a default.

Outputs are under `bin/Release/mac` or `bin/Release/win`, with metadata in the adjacent `metadata` folder. Building does not install the plugin or restart the host. The successful compilation has not yet been followed by a host loading test or an OpenHAB integration test.
