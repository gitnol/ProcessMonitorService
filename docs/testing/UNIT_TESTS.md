# Unit-Tests – ProcessMonitorService

Framework: xUnit | Mocking nach Bedarf (Moq oder NSubstitute) | Assertions mit xUnit-Bordmitteln oder einer
Apache-lizenzierten Bibliothek (FluentAssertions ab 8 lizenzpflichtig, `architecture/DEPENDENCIES.md`) — für die
PowerShell-Skripte Pester 5.

**Stand 2026-10-11:** Es gibt **keine** Tests. Pester-Tests entstehen ab I2, das xUnit-Projekt in I4. Bis dahin ist
Gate 4 nicht ausführbar; das steht so in `docs/CONVENTIONS.md` §11.

---

## Grundregeln

- Eine Test-Klasse pro Produktiv-Klasse: `ProcessFilterTests` testet `ProcessFilter`.
- Test-Methoden-Naming: `MethodUnderTest_State_ExpectedBehavior`.
- WMI/CIM, Dateisystem, Dienststeuerung und Zeit (Timer) werden in Unit-Tests **nicht** angesprochen — sonst
  Integrationstest. Dafür die reine Logik aus `ProcessMonitorWorker` herauslösen (I4) und `IProcessOwnerService` mocken.
- Ein Test = eine Verhaltens-Spezifikation. Keine Schleifen über Inputs ohne `[Theory]`.
- Bestandsverhalten zuerst als **Charakterisierungstest** festschreiben und per Mutation absichern
  (`KICKOFF.md` Phase 2), erst danach Verhalten ändern.

---

## Test-Projekt-Struktur (Ziel ab I4)

```
tests/
├── ProcessMonitorService.UnitTests/
│   ├── Filtering/
│   │   └── ProcessFilterTests.cs        # Include/Exclude, Wildcards, WQL-Erzeugung + Escaping
│   ├── Logging/
│   │   └── CommandLineMaskerTests.cs    # ab I5
│   ├── Worker/
│   │   └── ProcessSidCacheTests.cs      # Kapazitätsgrenze, Verdrängung, Ablauf (Zeitquelle injiziert)
│   └── ProcessMonitorService.UnitTests.csproj
└── Install.Tests.ps1                    # Pester, ab I2 (alles gemockt: Get-Acl, Get-Item, sc.exe)
```

---

## Test-Pattern (Arrange-Act-Assert)

```csharp
public sealed class ProcessFilterTests
{
    [Theory]
    [InlineData("chrome.exe", true)]
    [InlineData("CHROME_PROXY.EXE", true)]
    [InlineData("notepad_temp.exe", false)]   // Exclude *temp* hat Vorrang
    [InlineData("calc.exe", false)]           // nicht im Include-Filter
    public void ShouldMonitor_IncludeAndExclude_ExcludeWins(string processName, bool expected)
    {
        // Arrange
        var sut = new ProcessFilter(include: ["notepad.exe", "chrome*"], exclude: ["*temp*", "system*"]);

        // Act
        var result = sut.ShouldMonitor(processName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildWqlCondition_PatternWithApostrophe_EscapesWithBackslash()
    {
        var sut = new ProcessFilter(include: ["o'brien.exe"], exclude: []);

        var condition = sut.BuildWqlCondition();

        Assert.Contains(@"TargetInstance.Name = 'o\'brien.exe'", condition);
    }
}
```

(Beispielcode für das Zielbild aus I4 — `ProcessFilter` existiert noch nicht. Die Fälle entsprechen den README-Beispielen
der Filterlogik und sind damit das fachliche Orakel.)

---

## Was wird getestet

- **Filterlogik:** Exclude vor Include, leere Listen, Wildcards `*`/`?`, Groß-/Kleinschreibung, Sonderzeichen
- **WQL-Erzeugung:** Escaping von Apostroph/Backslash, `LIKE`-Metazeichen, leere Filter → keine Bedingung
- **SID-Cache:** Kapazitätsgrenze, Verdrängung des ältesten Eintrags, Ablauf nach `CacheExpiryMinutes`
- **Maskierung der Kommandozeile** (ab I5): alle unterstützten Muster, keine Veränderung harmloser Argumente
- **Options-Validierung** (ab I4): ungültige Intervalle/Muster werden abgelehnt
- **PowerShell:** Prüffunktionen in `install.ps1`/`Set-SecureACLs.ps1` (I2), CSV-Neutralisierung in `process-JSONFiles.ps1` (I7)

---

## Was NICHT in Unit-Tests gehört

- Echte WMI-Ereignisse, CIM-Abfragen, Dienstinstallation, ACL-Änderungen → `testing/INTEGRATION_TESTS.md`
- Reflektion-Magie zum Zugriff auf private Member — refaktoriere stattdessen (`internal` + `InternalsVisibleTo`)
- Mehrere Asserts pro Test ohne Zusammenhang

---

## Coverage-Ziel

- Reine Logik (Filter, Cache, Maskierung, Validierung): **≥ 80 %**
- `ProcessMonitorWorker`-Verdrahtung und `ProcessOwnerService`: Unit-Coverage niedriger ok; Hauptverifikation in
  Integrationstests auf einer Test-VM.

```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## Pflege

Trigger: neue öffentliche Methode in der Logik-Schicht → Tests hinzufügen.
