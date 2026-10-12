# Integration-Tests – ProcessMonitorService

Tests, die mehrere Komponenten zusammen mit echter Windows-Infrastruktur prüfen — hier: echtes WMI/CIM, echte
Dienststeuerung, echtes Dateisystem mit ACLs. Es gibt **keine** Datenbank und keine HTTP-Aufrufe.

**Stand 2026-10-11:** keine Integrationstests. Alle Tests unten brauchen Adminrechte und gehören auf eine **Test-VM**,
nicht auf Entwickler- oder Produktivrechner. Sie laufen nicht im Gate 4 (`[Trait("Category", "Integration")]` bzw.
Pester-Tag `Integration`, `docs/CONVENTIONS.md` §11/§12).

---

## Test-Projekt-Struktur (Ziel)

```
tests/
├── ProcessMonitorService.IntegrationTests/      # oder im Unit-Projekt mit Trait "Integration"
│   ├── Wmi/
│   │   └── ProcessEventCaptureTests.cs          # echter Watcher: notepad.exe starten/beenden → Ereignis
│   ├── Cim/
│   │   └── ProcessOwnerServiceTests.cs          # Owner-SID des eigenen Prozesses, PID 0, beendeter Prozess
│   └── ProcessMonitorService.IntegrationTests.csproj
└── Install.Integration.Tests.ps1                # Pester: install/uninstall auf Test-VM, ACL- und Besitzerprüfung
```

---

## WMI-Ereignisse end-to-end

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task Worker_NotepadStartedAndStopped_LogsStartAndStopWithSameSid()
{
    // Arrange: Host mit Worker, Include-Filter "notepad.exe", Serilog in eine Temp-Datei (CLEF)
    using var tmp = new TempDirectory();
    using var host = TestHostBuilder.Build(include: ["notepad.exe"], logPath: tmp.Path);
    await host.StartAsync();

    // Act: Prozess starten, > 2 s laufen lassen (Polling WITHIN 1), beenden
    using var process = Process.Start(new ProcessStartInfo("notepad.exe"));
    await Task.Delay(TimeSpan.FromSeconds(3));
    process!.Kill();
    await Task.Delay(TimeSpan.FromSeconds(3));
    await host.StopAsync();

    // Assert: genau ein Start- und ein Stop-Ereignis mit derselben PID und SID
    var events = ClefReader.Read(tmp.Path).Where(e => e.ProcessId == process.Id).ToList();
    Assert.Equal(["Start", "Stop"], events.Select(e => e.EventType));
    Assert.Equal(events[0].UserSid, events[1].UserSid);
}
```

(Zielbild; `TestHostBuilder`, `TempDirectory` und `ClefReader` sind noch zu schreibende Test-Helfer.) Wichtig: Wartezeiten
größer als das Polling-Intervall wählen; Prozesse unter einer Sekunde Laufzeit sind bekanntermaßen nicht erfassbar (K1)
und kein Testfehler.

---

## Installation und ACLs (Pester, Test-VM)

- `install.ps1` aus `C:\Program Files\ProcessMonitorService` → Dienst `Running`, `sc.exe qc` zeigt LocalSystem und den
  erwarteten Pfad.
- Nach der Installation: Besitzer von `C:\ProgramData\ProcessMonitorService` = Administratoren, nur zwei ACEs
  (`S-1-5-32-544`, `S-1-5-18`), Vererbung aus.
- Negativfall (ab I2): Verzeichnis vorher als Standardbenutzer angelegt → Installation bricht ab.
- Negativfall (ab I2): `-ExePath` in einem für Benutzer schreibbaren Ordner → Installation bricht ab.
- `uninstall.ps1` → Dienst entfernt, Datenverzeichnis bleibt.

---

## Remote-Deployment

Manuell zwischen zwei Test-VMs (WinRM aktiv), Checkliste in `architecture/REMOTE_EXECUTION.md`. Keine Automatisierung
geplant, solange das Deploy-Skript als „nicht optimiert“ gilt (I7).

---

## CI-Strategie

- Heute kein CI. Ziel (Backlog): Unit-Tests + PSScriptAnalyzer bei jedem Push auf einem Windows-Runner.
- Integrationstests nur manuell bzw. in einem eigenen Job auf einem Windows-Runner mit Adminrechten (z. B. vor
  Releases); Ergebnisse generisch im CHANGELOG, Details mit Rechnerbezug in `docs/local/`.

---

## Pflege

Trigger: neue Ereignisquelle / neue CIM-Abfrage / Änderung an Install- oder Deploy-Skript → Integrationstest
ergänzen oder Checkliste anpassen.
