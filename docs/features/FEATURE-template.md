# Feature: [Feature-Name]

---

## Zweck

[Was macht dieses Feature? Welche Eingabe? Welche Ausgabe? Welcher Auslöser?]

---

## Einstiegspunkte

| Auslöser | Typ | Beschreibung |
|---|---|---|
| `--[option] <wert>` | CLI-Argument | [Beschreibung] |
| `[WorkerName]` | Hosted-Service / Worker | [Beschreibung, z.B. „läuft per Task Scheduler / IHostedService"] |

---

## Eingabe-/Ausgabe-Kontrakt

| Parameter | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `[param]` | `string` / `int` / `FileInfo` | Ja / Nein | [Bedeutung] |

**Ausgabe:** [Beschreibung — Exitcode, Datei, Stdout, Return-Wert]

---

## Service-Schicht

```csharp
public interface I[Feature]Service
{
    Task<[Result]> RunAsync([Input] input, CancellationToken ct);
}
```

---

## Exitcodes / Statuscodes

| Code | Bedeutung |
|---|---|
| `0` | Erfolg |
| `1` | Allgemeiner Fehler |
| `[N]` | [Bedeutung] |

---

## Kritische Patterns

- [Pattern — z.B. „CancellationToken an alle async-Aufrufe weiterreichen"]
- [Pattern — z.B. „IOptions<T> für Konfiguration, kein statischer Zugriff"]

---

## Bekannte Einschränkungen

| Einschränkung | Ursache | Status |
|---|---|---|
| [Beschreibung] | [Ursache] | [Geplant I# / Akzeptiert] |

---

## Bekannte Fallstricke

- [Fallstrick — z.B. „Hosted-Service ignoriert Exceptions ohne expliziten try/catch im ExecuteAsync"]

---

## Teststrategie

- Unit: `tests/[Projekt].UnitTests/[Feature]ServiceTests.cs` — Service isoliert (Mock-Dependencies)
- Integration: `tests/[Projekt].IntegrationTests/[Feature]PipelineTests.cs` — echte DB / echtes Filesystem
- End-to-End: ggf. via CLI-Prozessaufruf mit `Process.Start` und Exitcode-Prüfung
