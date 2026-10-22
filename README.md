# Loupedeck OpenHAB Plugin NG

OpenHAB NG verbindet Loupedeck CT, Live, Live S und Razer Stream Controller mit OpenHAB. Die NG-Version verwendet .NET 8 und hat eine eigene Identität; sie kann neben dem ursprünglichen OpenHAB-Plugin eingerichtet werden.

## Funktionen

- Initialer Abruf aller Items über REST, Live-Aktualisierung durch WebSocket-Events und automatischer Neuaufbau nach Verbindungsabbruch.
- WebSocket-Endpunkte `/ws/events` und als Fallback `/ws` für ältere OpenHAB-Versionen; Heartbeat alle fünf Sekunden.
- REST-Abgleich alle fünf Sekunden ohne WebSocket, sonst alle 30 Sekunden zur Korrektur verpasster Ereignisse.
- Schalter mit zustandsabhängigen Symbolen; automatische Lampen-/Steckdosenauswahl anhand der OpenHAB-Icon-Kategorie sowie explizite Aktionen **Lamp ON/OFF** und **Socket ON/OFF**.
- Dimmer mit Prozentanzeige, Drehgeber zur Einstellung von 0 bis 100 und Tastendruck zum Ein-/Ausschalten.
- Rollladensteuerung mit drei vertikal untereinander platzierbaren Widgets **AUF**, **STOP**, **AB**. Jedes zeigt die gemeldete Position und sendet den jeweiligen Befehl an dasselbe ausgewählte `Rollershutter`-Item.
- Live-Zustandsanzeige für beliebige Items und ein Widget für den WebSocket-Verbindungsstatus.

## Build-Anleitung

### Voraussetzungen

1. Ein .NET SDK ab Version 8 mit den .NET-8-Zielpaketen; für die Integrationstests zusätzlich die .NET-8-/ASP.NET-Core-8-Laufzeit. Vorhandene SDKs mit `dotnet --list-sdks` und Laufzeiten mit `dotnet --list-runtimes` prüfen.
2. Eine **zu .NET 8 kompatible `PluginApi.dll`** aus dem Loupedeck-/Logi-SDK bzw. einer entsprechenden Host-Installation.
3. Zugriff auf NuGet für Newtonsoft.Json 13.0.3 oder ein lokaler Paketcache.

Das installierte LogiPluginService auf dem hier verwendeten Mac liefert bereits eine **.NET-10-API**. Diese DLL ist keine passende Referenz für das `net8.0`-Projekt. Als geprüfte .NET-8-Referenz dient lokal die API aus dem vorhandenen ATEM-Build. Das ATEM-Repository wird dadurch nicht verändert und ist keine fest eingebaute Projektabhängigkeit.

### macOS: geprüfter lokaler Build

Im Terminal zunächst in das Repository wechseln:

```bash
cd /Users/tseiman/Downloads/LoupedeckOpenHABPlugin-NG
```

Mit normaler Paketwiederherstellung bauen:

```bash
dotnet build OpenHABPlugin.sln --configuration Release -p:PluginApiDir=/Users/tseiman/Downloads/LoupedeckAtemControlerPlugin/bin/Debug/mac
```

Alternativ eine andere kompatible SDK-Installation verwenden:

```bash
dotnet build OpenHABPlugin.sln --configuration Release -p:PluginApiDir="/Pfad/zum/kompatiblen/SDK"
```

`PluginApiDir` bezeichnet das Verzeichnis, das `PluginApi.dll` enthält. Standardmäßig wird unter macOS `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/` und unter Windows `C:/Program Files/Logi/LogiPluginService/` gesucht. Bei einer dort installierten neueren API muss der passende Pfad explizit angegeben werden. Das Pluginziel bleibt .NET 8 entsprechend der ATEM-Vorlage.

### Offline-Build mit dem vorhandenen Cache

Auf diesem Mac kann Newtonsoft.Json ohne Netzwerk aus dem bereits vorhandenen NuGet-Paket wiederhergestellt werden:

```bash
dotnet restore OpenHABPlugin.sln --source /Users/tseiman/.nuget/packages/newtonsoft.json/13.0.3 --packages ./packages
```

Anschließend ohne erneute Wiederherstellung bauen:

```bash
dotnet build OpenHABPlugin.sln --configuration Release --no-restore -p:PluginApiDir=/Users/tseiman/Downloads/LoupedeckAtemControlerPlugin/bin/Debug/mac
```

### Windows

Im Repository-Verzeichnis und mit einem kompatiblen SDK-Pfad:

```powershell
dotnet build OpenHABPlugin.sln --configuration Release -p:PluginApiDir="C:/Pfad/zum/kompatiblen/SDK"
```

### Ergebnis und Installation

Der Build erstellt folgende Struktur:

```text
bin/Release/
  mac/                         # auf Windows: win/
    OpenHABPluginNG.dll
    OpenHABPluginNG.deps.json
    Newtonsoft.Json.dll
  metadata/
    LoupedeckPackage.yaml
    Icon*.png
```

Die Host-API wird nicht mit ausgeliefert. Debug-Ausgaben liegen entsprechend unter `bin/Debug/`. Der Build installiert das Plugin nicht automatisch und startet keinen Host neu. Das Paket muss unter dem neuen Namen **OpenHABNG** geladen werden; bei einer lokalen Entwicklungsinstallation muss der Plugin-Link auf das Verzeichnis zeigen, das `mac` bzw. `win` und `metadata` enthält, also beispielsweise `bin/Release`.

Die Kompilierung gegen die ATEM-API ist lokal geprüft. Das Laden im aktuell installierten .NET-10-Host und die Anzeige auf echter Hardware müssen separat geprüft werden; ein erfolgreicher Build allein belegt diese Kompatibilität nicht. Der Build entfernt alte `OpenHABPlugin.dll`-Ausgaben im lokalen Zielordner; sie gehören nicht in ein NG-Paket.

## OpenHAB einrichten

1. Im Host das Plugin **OpenHAB NG** auswählen.
2. Die Aktion **Set OpenHAB URL** auf eine Taste ziehen, die Server-URL eintragen (etwa `http://openhab.local:8080`) und einmal ausführen. Basis-Pfade hinter einem Reverse Proxy werden unterstützt.
3. Falls erforderlich, **Set OpenHAB API token** ausführen. Der Token wird für REST und WebSocket verwendet. Er steht als Profilparameter und im lokalen `config.json`; die Konfiguration entsprechend behandeln. Alternativ den Token mit `mcedit` direkt im NG-Konfigurationsverzeichnis bearbeiten, ohne ihn als Tastenparameter zu hinterlegen.
4. Nach dem ersten Item-Abruf die gewünschten Aktionen auswählen. Falls ein bereits geöffnetes Auswahlmenü noch leer ist, dieses erneut öffnen.

Die NG-Konfiguration verwendet den vom Host für **OpenHABNG** bereitgestellten Datenordner. Das alte Plugin und seine Konfiguration werden nicht automatisch migriert. Eine eigene `config.json` enthält beispielsweise:

```json
{
  "url": "http://openhab.local:8080",
  "token": ""
}
```

Beim Ändern der URL bleibt der Token erhalten; beim erneuten Konfigurieren wird die bisherige Verbindung beendet. Tokens werden nicht in Diagnoseausgaben geschrieben.

## Aktionen und Anzeigen

**Switch (automatic icon)** verwendet die Item-Kategorie: `light`, `lamp` und `bulb` ergeben das Lampensymbol; Kategorien mit `socket` oder `outlet` das Steckdosensymbol. Andere Kategorien erhalten einen allgemeinen Schalter. Mit **Lamp ON/OFF** bzw. **Socket ON/OFF** lässt sich das Symbol unabhängig von der Kategorie festlegen. Die Symbole werden bevorzugt direkt von OpenHAB über `/icon/<Kategorie>?format=svg&anyFormat=true&state=<Zustand>&iconset=classic` geladen und behalten die dort definierten Farben. Für Licht entspricht OFF dem Wert 0 und ON dem Wert 100; Dimmer verwenden ihren tatsächlichen Prozentwert, Steckdosen ON/OFF. Bei fehlender Kategorie, unbekanntem Zustand oder Abrufproblemen dienen die lokalen gelben/grauen Symbole als Ersatz. Unbekannte Werte werden als `—` angezeigt. Der gemeldete Serverzustand ist maßgeblich, ein Tastendruck täuscht keinen bestätigten Zustand vor.

**Dimmer (%)** zeigt Zahlen als Prozent, `ON` als `100 %`, `OFF` als `0 %` und `NULL`/`UNDEF` als `—`. Auch das OpenHAB-Icon folgt dem Prozentwert, beispielsweise mit `state=36`. Drehbewegungen sind auf 0 bis 100 begrenzt. Schnelle Drehbewegungen werden auch bei verzögerter Rückmeldung aufaddiert.

Für den **Rollladen** drei Tasten vertikal untereinander mit **Rollladen AUF**, **Rollladen STOP**, **Rollladen AB** belegen und jeweils dasselbe Item wählen. Das ist eine Gruppe aus drei frei platzierbaren Widgets; es wird kein fertiges dreifeldriges Layout automatisch im Profil angelegt. Die drei Symbole sind lokal eingebettete originale Framework7-SVGs: AUF verwendet `arrowtriangle_up` in Grau, STOP das rote `stop`-Quadrat, AB `arrowtriangle_down` in Grau, entsprechend der Web-UI-Vorlage. Dafür ist weder ein Webabruf noch eine installierte F7-Schrift erforderlich. Die vertikale Anordnung nimmt der Benutzer im Profil vor. Bei OpenHAB entspricht 0 % vollständig geöffnet und 100 % vollständig geschlossen.

**Live state** zeigt den aktuellen Item-Wert. OpenHAB-spezifische Java-/printf-Formatierungen werden nicht als C#-Formatstrings ausgeführt; Einheiten bleiben im Rohwert erhalten.

**Connection status** zeigt `LIVE`, wenn die WebSocket-Verbindung steht, andernfalls `REST / OFFLINE`. Die Statusanzeige allein bestätigt keine Erreichbarkeit der einzelnen Geräte. Verbindungs- und HTTP-Probleme erscheinen im Host-Log mit Präfix `[OpenHAB NG]`.

## Icon-Abruf und Cache

Die Tastenanzeige wartet nicht auf Netzwerkanfragen: SVGs werden im Hintergrund geladen und lösen nach dem Eintreffen eine erneute Anzeige aus. Gleiche Kategorie/Zustand-Kombinationen teilen einen Abruf. Der Speicher-Cache umfasst höchstens 128 Varianten, verwendet höchstens vier parallele Downloads und erneuert erfolgreiche Einträge bei Bedarf nach fünf Minuten. Fehlgeschlagene Abrufe werden frühestens nach 30 Sekunden erneut versucht. Cache und Downloads werden beim URL-Wechsel bzw. Entladen beendet. Der konfigurierte API-Token wird auch für die Icons verwendet.

Der Abruf verwendet den konfigurierten OpenHAB-Server, keinen im Code fest eingebauten privaten Hostnamen.

## Integrationstests

Die Tests verwenden einen lokalen OpenHAB-Testserver auf einem zufälligen Loopback-Port. Sie schalten keine realen Geräte und benötigen keine PluginApi.dll. Aus dem Repository-Verzeichnis:

```bash
dotnet run --project tests/OpenHAB.IntegrationTests.csproj
```

Offline auf diesem Mac:

```bash
dotnet restore tests/OpenHAB.IntegrationTests.csproj --source /Users/tseiman/.nuget/packages/newtonsoft.json/13.0.3 --packages ./packages
```

```bash
dotnet run --project tests/OpenHAB.IntegrationTests.csproj --no-restore
```

Geprüft werden REST-/WebSocket-Authentifizierung, initiale Zustände, fragmentierte Events, Heartbeats, Zustandsänderungen und Anzeige-Benachrichtigungen, Schutz vor veralteten REST-Antworten, AUF/STOP/AB-Befehle, HTTP-Fehler, Prozentwerte, Wiederverbindung, Legacy-Endpunkt, REST-Fallback und das Beenden der Hintergrundabfragen. Die Tests ersetzen keine Prüfung der tatsächlichen Darstellung im Loupedeck-/Logi-Host.

## Identität, Herkunft und Lizenz

Plugin-ID: `OpenHABNG`, Anzeigename: `OpenHAB NG`, Assembly: `OpenHABPluginNG.dll`, Namespace: `Loupedeck.OpenHABPluginNG`.

Diese Weiterentwicklung basiert auf [Loupedeck_OpenHABPlugin von Arno Pernozzoli](https://github.com/pernozzoli/Loupedeck_OpenHABPlugin). Die ursprünglichen Lizenz- und Copyright-Angaben bleiben erhalten. Änderungen und Prüfergebnisse stehen in [ChangeLog.md](ChangeLog.md). Lizenz: MIT, siehe [LICENSE](LICENSE).

Protokollreferenzen: [OpenHAB WebSocket API](https://www.openhab.org/docs/configuration/websocket.html), [OpenHAB 5.0 WebSocket API](https://v50.openhab.org/docs/configuration/websocket.html), [Item-Typen](https://www.openhab.org/docs/configuration/items.html).

Optionaler Live-Test nur für SVG-Abrufe, ohne Item-Abfragen oder Gerätebefehle auf dem angegebenen Server:

```bash
OPENHAB_ICON_SERVER=https://openhab.tsei.mdn:8443 dotnet run --project tests/OpenHAB.IntegrationTests.csproj --no-restore -- --icons-only
```

Auf diesem Mac liefert der direkte HTTPS-Abruf über curl gültige SVGs, während die installierten .NET-8- und .NET-9-Laufzeiten am Server mit `AppleCrypto: bad certificate format` scheitern. Die Zertifikatsprüfung bleibt aktiviert. Die Ursache und das Verhalten im tatsächlichen Logi-Host müssen noch geprüft werden; bis dahin stehen dort gegebenenfalls nur die lokalen Ersatzsymbole zur Verfügung.

Die F7-Symbole stammen aus [Framework7 Icons](https://github.com/framework7io/framework7-icons). Die MIT-Lizenz und Herkunftsnachweise unter `licenses/` werden in die Build-Ausgabe übernommen.
