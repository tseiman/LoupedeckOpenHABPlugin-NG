# ChangeLog

## 2026-10-23 – OpenHAB NG: Live-Zustände und neue Bedienelemente

### Ausgangsfehler

- WebSocket-Verbindung beim Initialisieren auskommentiert; Receive-Schleife verließ die Verarbeitung vor dem Auswerten abgeschlossener Nachrichten. Heartbeat-Timer wurde nicht gestartet, Authentifizierung nicht verwendet und fragmentierte Nachrichten nicht zusammengesetzt.
- Events verwendeten Item-Namen, während Profile absolute URLs speicherten. Schalter wechselten durch blindes Toggeln statt durch Übernahme des gemeldeten Zustands; ein unbekannter Zustand konnte so ebenfalls einen Zustandswechsel auslösen.
- Polling war nur für registrierte Items aktiv, lief als unbeaufsichtigte async-void-Schleife und wurde beim Entladen nicht beendet. Die Label-Auswahl mischte unterschiedliche Item-Mengen und konnte an fehlenden Gruppen scheitern.
- Das Setzen einer neuen URL initialisierte weiterhin den alten URL-Wert und konnte den Token aus der Konfiguration entfernen.

### Verbindung und Zustandsmodell

- Service mit explizitem Lebenszyklus und abbrechbaren Hintergrundaufgaben neu aufgebaut. Ein erneutes Konfigurieren beendet die vorherige Session; Entladen beendet HTTP-Abfragen, Polling und WebSocket.
- Initialer REST-Abruf aller Items, dann WebSocket über `/ws/events`, bei Bedarf `/ws` für ältere Server. OpenHAB sendet beim Verbinden standardmäßig alle Events; eine zusätzliche Subscribe-Nachricht ist dafür nicht erforderlich.
- Token für REST per Bearer-Header und für WebSocket über die dokumentierten Subprotokolle. Keine Tokens oder kompletten Event-Nutzdaten in Diagnoseausgaben.
- Heartbeat alle fünf Sekunden, Empfangs-Timeout, Wiederverbindung mit begrenzter Wartezeit, Reconciliation nach Verbindung sowie Verarbeitung vollständig zusammengesetzter Textnachrichten bis 1 MiB.
- `ItemStateEvent` und `ItemStateChangedEvent` aktualisieren den bestätigten Cache und lösen Anzeigen-Benachrichtigungen aus. Fehlerhafte, fremde und reine Command-Events ändern die Anzeige nicht; doppelte Zustände lösen kein blindes Toggle aus.
- REST-Abgleich alle fünf Sekunden ohne WebSocket und alle 30 Sekunden mit WebSocket. Veraltete REST-Antworten überschreiben keine neueren Events; entfernte Items werden als unbekannt gemeldet. Antworten einer abgelösten Session werden verworfen.
- Neue Profilparameter verwenden stabile Item-Namen statt servergebundener URLs. REST-Zielpfade werden aus der konfigurierten Serveradresse gebildet. HTTP-Fehler werden geprüft; gesendete Befehle werden nicht als physisch bestätigte Zustände ausgegeben.
- Cookie-Verwaltung für die tokenbasierte REST-API deaktiviert; damit funktioniert der Client auch in Umgebungen ohne Domainnamen-Abfrage.

### Bedienung und Darstellung

- Live-Schalter-Widgets mit echten AN/AUS-Zuständen, automatischen Symbolen aus der Item-Kategorie sowie expliziten Lampen- und Steckdosenaktionen. Lampen AN erhalten zusätzlich Strahlen; AN ist gelb, AUS grau. Kein Netzabruf beim Rendern der Tastenbilder.
- Dimmer: Prozentwerte auch für ON/OFF, sichere Anzeige für NULL/UNDEF, Begrenzung auf 0–100, kulturunabhängige Zahlen und Aufaddieren schneller Drehbewegungen bei verzögertem Feedback. Änderungen aktualisieren sowohl Tastenbild als auch Drehgeberwert.
- Rollladen: drei frei platzierbare Widgets AUF/STOP/AB für dasselbe Rollershutter-Item, mit aktueller Positionsanzeige und den OpenHAB-Befehlen UP/STOP/DOWN. Kein automatisch angelegtes mehrzelliges Profillayout.
- Live-Zustandsanzeige für alle Items, gruppierte Auswahl ohne doppelte Others-Gruppe, sichere Anzeige bei fehlenden Labels und Verbindungsstatus-Widget.
- Anzeigen abonnieren Zustandsereignisse beim Laden und melden sich beim Entladen wieder ab.
- Aktionen für Server-URL und API-Token; URL-Wechsel erhält den Token. Anmeldedaten können alternativ direkt im NG-config.json hinterlegt werden.

### Getrennte NG-Identität

- Paketname `OpenHABNG`, Anzeigename `OpenHAB NG`, Assembly `OpenHABPluginNG.dll`, Namespace `Loupedeck.OpenHABPluginNG`, eigene Assembly-GUID und Version 0.2.0.
- Plugin-Name trennt Profile und den vom Host verwalteten Datenordner vom ursprünglichen OpenHAB-Plugin. Keine automatische Übernahme oder Änderung der alten Konfiguration.
- Alte OpenHABPlugin-Binaries im lokalen Build-Ausgabeordner werden beim Bauen entfernt, damit sie nicht versehentlich zusammen mit der NG-Version paketiert werden.
- Windows-Metadatenpfad aktiviert; geprüft wurde der macOS-Release-Build, kein Windows-Laufzeittest.

### Dokumentation und Validierung

- Haupt-README mit vollständiger Build-Anleitung für macOS und Windows, geprüftem ATEM-SDK-Pfad, Offline-Restore, Ausgabe-/Installationsstruktur, Einrichtung, Widget-Verwendung und Testaufrufen aktualisiert.
- Release-Build auf .NET 8 mit der API aus dem ATEM-Build erfolgreich, ohne Compilerwarnungen oder Fehler. Die aktuell installierte .NET-10-Logi-API wurde nicht als Referenz verwendet.
- Neue ausführbare Integrationstests unter `tests/` verwenden einen echten lokalen HTTP-/WebSocket-Testserver und benötigen keine PluginApi.dll. Geprüft: REST-/WebSocket-Authentifizierung, Snapshot, Heartbeats, fragmentierte Nachrichten, Events, UI-Benachrichtigungsparameter, doppelte/ungültige Events, Schalter-/Rollladenbefehle, HTTP-Fehler, Race zwischen REST und WebSocket, Prozentformatierung, Wiederverbindung, Legacy-Endpunkt, Rekonfiguration, Polling-Fallback und vollständiges Beenden.
- Kein Live-Test gegen die Installation des Benutzers und keine Prüfung der nativen Darstellung auf echter Loupedeck-/Logi-Hardware. Der erfolgreiche Test von Benachrichtigungen bestätigt den Datenweg bis zur Aktion, nicht das tatsächliche Zeichnen durch den Host. Insbesondere das Laden im installierten .NET-10-Host bleibt separat zu prüfen.
- Protokollreferenzen: https://www.openhab.org/docs/configuration/websocket.html und https://v50.openhab.org/docs/configuration/websocket.html.


## 2026-10-22 – Migration auf .NET 8 nach ATEM-Vorbild

### Aktueller Stand

- Maßgebliche Vorlage ist das moderne ATEM-Plugin: SDK-Projektformat (`Microsoft.NET.Sdk`), `TargetFramework=net8.0`, NuGet über `PackageReference`. Der vorherige Ansatz, .NET Framework 4.8 beizubehalten, wurde verworfen.
- Alte Framework-Referenzen und explizite C#-Dateilisten entfernt; das SDK erfasst Quellcode automatisch. Newtonsoft.Json 13.0.3 wird als PackageReference wiederhergestellt und mit `CopyLocalLockFileAssemblies=true` in die Ausgabe kopiert.
- `packages.config` und die Framework-spezifische `App.config` mit alten Binding-Redirects entfernt.
- `GenerateAssemblyInfo=false` erhält die vorhandenen Assembly-Attribute ohne doppelte Generierung; Assemblyname und Namespace bleiben erhalten. Das eingebettete PNG wird weiterhin eingebunden.
- Die nicht mehr benötigten Imports `System.Runtime.Remoting.Messaging`, `System.Security.Policy` und `Loupedeck.Devices.Loupedeck2Devices` entfernt, damit der vorhandene Quellcode unter .NET 8 kompiliert.
- Gemeinsamer Zwischenablagepfad in `Directory.Build.props` früh gesetzt. Build-Ausgaben liegen unter `bin/<Konfiguration>/mac` bzw. `win`, Metadaten daneben.
- Wie im ATEM-Projekt zeigen die Standard-SDK-Pfade auf LogiPluginService. `PluginApiDir` bleibt überschreibbar, und die Host-API wird nicht mit ausgeliefert.

### Build und verbleibende Prüfung

- Paketwiederherstellung offline erfolgreich aus dem lokalen Cache von Newtonsoft.Json 13.0.3.
- Release-Build mit der .NET-8-API aus `/Users/tseiman/Downloads/LoupedeckAtemControlerPlugin/bin/Debug/mac` erfolgreich, keine Compilerfehler. Beim vollständigen Kompilieren vier bestehende Nullability-Warnungen (CS8622 und CS8604); ein unveränderter inkrementeller Build war ebenfalls erfolgreich.
- Ergebnis: `bin/Release/mac/OpenHABPlugin.dll`, Newtonsoft.Json, Abhängigkeitsbeschreibung und kopierte Metadaten.
- Build-Aufruf im Repository: `dotnet build OpenHABPlugin.sln --configuration Release -p:PluginApiDir=/Users/tseiman/Downloads/LoupedeckAtemControlerPlugin/bin/Debug/mac`. Für die lokale Offline-Prüfung wurde zuvor mit `dotnet restore OpenHABPlugin.sln --source /Users/tseiman/.nuget/packages/newtonsoft.json/13.0.3 --packages ./packages` wiederhergestellt; danach wurde mit `--no-restore` gebaut.
- Der installierte Logi-Host stellt inzwischen eine .NET-10-API bereit. Diese wurde für den .NET-8-Build nicht verwendet. Erfolgreiche Kompilierung gegen die ATEM-API ersetzt keinen Ladetest im installierten Host oder Funktionstest gegen OpenHAB; diese Tests stehen aus.

## 2026-10-22 – Verworfener Zwischenstand: Build-Konfiguration auf 4.8

Dieser Ansatz wurde nach Korrektur durch den Benutzer verworfen. Maßgeblich ist die folgende dokumentierte Migration auf .NET 8 nach ATEM-Vorbild. Die Angaben in diesem Abschnitt beschreiben ausschließlich den damaligen Zwischenstand.

### Korrekturen

- Projektziel bleibt .NET Framework 4.8. `packages.config` verwendet nun `net48` statt `net6.0`; `App.config` nennt ebenfalls v4.8 statt v4.7.2. Die Newtonsoft.Json-Assembly unter `lib/net45` ist damit kompatibel und bleibt unverändert.
- `Directory.Build.props` verwendet nun wie das Projekt `latestmajor` statt der widersprüchlichen Vorgabe 7.3.
- Doppelte und ausschließlich für macOS definierte Ausgabepfade entfernt. Debug und Release bauen lokal nach `bin/<Konfiguration>/mac` bzw. `bin/<Konfiguration>/win`; ein Debug-Build schreibt nicht mehr direkt in die Plugin-Installation. Basisverzeichnisse sind auch beim direkten Projekt-Build ohne Solution definiert.
- IDE-spezifische Debug-Befehle zum Beenden von Loupedeck entfernt.

### Aus dem ATEM-Plugin übernommene Verbesserungen

Verglichen wurden insbesondere `src/LoupedeckAtemControlerPlugin/LoupedeckAtemControlerPlugin.csproj` und `src/Directory.Build.props` aus `/Users/tseiman/Downloads/LoupedeckAtemControlerPlugin`. Das ATEM-Projekt wurde nicht verändert.

- SDK-Verzeichnis über `PluginApiDir` konfigurierbar; die bisherigen Loupedeck-Pfade bleiben Standard. `PluginApi.dll` wird mit `Private=False` nicht in das Plugin kopiert, da sie vom Host bereitgestellt wird.
- Frühe, verständliche Fehlermeldung bei fehlender SDK-Datei. Ein abweichender kompatibler SDK-Pfad lässt sich mit `/p:PluginApiDir=/Pfad/zum/SDK` übergeben.
- Metadaten werden durch ein plattformübergreifendes MSBuild-Copy-Ziel einschließlich Unterordnern kopiert. Die bisherigen Shell-/Batch-PostBuild-Aufrufe entfallen; die alten Skripte bleiben unbenutzt im Repository.

### Build-Versuche und Prüfung

- Ausgangsbuild mit Mono MSBuild scheiterte an fehlender PluginApi und fehlendem Newtonsoft.Json-Paket.
- Newtonsoft.Json 13.0.3 aus dem vorhandenen lokalen NuGet-Cache nach `packages/Newtonsoft.Json.13.0.3` wiederhergestellt; Paketdateien bleiben durch die vorhandene Git-Ausnahme lokal. Für einen frischen Checkout ist weiterhin eine NuGet-Wiederherstellung notwendig.
- Erneuter Release-Build: blockiert durch fehlendes kompatibles Loupedeck-SDK unter `/Applications/Loupedeck.app/Contents/MonoBundle/PluginApi.dll`. Mono enthält die .NET-Framework-4.8-Referenzassemblies.
- Die installierte API unter `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll` trägt das Zielframework `.NETCoreApp,Version=v10.0`; die API aus dem ATEM-Build-Verzeichnis trägt v8.0. Beide sind kein Ersatz für eine .NET-Framework-4.8-API. Installierte dotnet-SDKs reichen hier bis Version 9.0.201.
- XML-Konfigurationen erfolgreich geparst; das Metadaten-Ziel separat ausgeführt und die kopierte YAML-Datei bytegleich geprüft. Kein erfolgreicher Plugin-Build und kein Laufzeittest.
- Build-Aufruf: `/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild OpenHABPlugin.sln /t:Build /p:Configuration=Release /verbosity:minimal /nologo` (im Repository-Verzeichnis).

### Weitere Modernisierungsmöglichkeiten

- Das ATEM-Plugin verwendet SDK-Projektformat, `PackageReference` und net8.0. Diese Umstellung wäre für OpenHAB sinnvoll, muss aber mit der tatsächlich verwendeten Host-Version abgestimmt werden; der aktuell installierte Host liefert bereits eine .NET-10-API. Nicht ungeprüft auf net8.0 umgestellt.
- Eine Migration auf den aktuellen Logi-Host benötigt ein passendes .NET-10-SDK sowie eine Prüfung und gegebenenfalls Anpassung der verwendeten Plugin-API und alten Framework-Namensräume.
- Die automatische Plugin-Link-Installation des ATEM-Projekts wurde nicht übernommen, weil der Zielhost noch nicht festgelegt ist. Web-Konfiguration und Shared-State-Helfer sind mögliche spätere Funktionserweiterungen und für die Build-Reparatur nicht erforderlich.

## 2026-10-22 – Übernahme der manuellen Änderungen

Quelle: `/Users/tseiman/Downloads/Loupedeck_OpenHABPlugin`.
Ziel: `/Users/tseiman/Downloads/LoupedeckOpenHABPlugin-NG`.
Das Datum bezeichnet die Übernahme, nicht den ursprünglichen Änderungszeitpunkt.

### Übernommene Änderungen

- `OpenHABPlugin/OpenHABPlugin.csproj`: .NET Framework von 4.7.2 auf 4.8 angehoben.
- C#-Spracheinstellung für Debug und Release von `8.0` auf `latestmajor` geändert; die Hauptversion richtet sich damit nach dem installierten Compiler.
- Für den Debug-Nachbau-Befehl `killall LoupedeckConfig || true` die Einstellung `externalConsole=True` ergänzt.
- Newtonsoft.Json-Referenzpfad von 13.0.2 auf 13.0.3 aktualisiert; die Assembly stammt weiterhin aus `lib/net45`.
- `OpenHABPlugin/packages.config`: Newtonsoft.Json von 13.0.2 auf 13.0.3 aktualisiert und `targetFramework` von `net472` auf `net6.0` geändert, exakt wie in der manuellen Version.

### Abgleich und Hinweise

- Beide geänderten Projektdateien wurden bytegleich aus der manuellen Version übernommen und als gültiges XML geprüft.
- C#-Quellcode und übrige gemeinsame Projektdateien waren identisch; keine zusätzlichen Funktionsänderungen gefunden.
- Lokale IDE-Daten (`.vs`), Build-Ausgaben (`bin`, `obj`), `.DS_Store` und heruntergeladene NuGet-Pakete (`packages`) wurden nicht übernommen.
- Git-Verwaltung und Remote des Zielprojekts bleiben erhalten. Die Umbenennung betrifft im vorliegenden Stand den Repository-Namen; Assembly und Namespace heißen in beiden Versionen weiterhin `OpenHABPlugin` bzw. `Loupedeck.OpenHABPlugin`.
- Der übernommene Paketwert `targetFramework=net6.0` weicht von der Projektzielplattform .NET Framework 4.8 ab. Er wurde originalgetreu beibehalten; dies ist keine Migration auf .NET 6.
- Ein vollständiger Build und ein Funktionstest mit Loupedeck/OpenHAB wurden bei dieser Übernahme nicht durchgeführt.
