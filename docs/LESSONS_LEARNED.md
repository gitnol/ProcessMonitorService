# Lessons Learned – ProcessMonitorService

Strukturierter Index für gelernte Patterns. Jeder Eintrag benennt Scope und
Backport-Ziel, damit die Reflexions-Phase (REFLECTION.md Schritt 3) offene
generische Patterns als Backport-Vorschlag melden kann; eingespielt wird erst nach Freigabe
des Prompters (REFLECTION.md Schritt 3, Freigabe-Disziplin).

Neue Einträge entstehen aus dem „Lessons Learned"-Abschnitt der `CHANGELOG.md` —
dort kurze Beschreibung, hier der strukturierte Steckbrief.

---

I1 hat nur die Doku-Baseline eingeführt; Beobachtungen zum Template stehen als Stack-Lücken in `BACKLOG.md`, nicht
als Lesson.

## L1: Patchstand self-contained gebündelter Runtimes am Build prüfen, nicht am Scanner
- Inkrement: I8
- Scope: generisch-stack
- Backport-Ziel: `stacks/csharp-console/` (Abschnitt Deployment/Security, MSBuild-Target-Vorlage)
- Status: offen

`dotnet list package --vulnerable` meldet den Runtime-Pack einer self-contained Auslieferung nicht. Ein MSBuild-Target
nach `ResolveFrameworkReferences` vergleicht `ResolvedRuntimePack` (`Microsoft.NETCore.App`) mit einer
Mindestversion und bricht fail-closed ab; Nachweis zusätzlich am Artefakt (`deps.json`), weil pauschale „mit dem
Neubau erledigt"-Aussagen (hier MsQuic) dort widerlegt werden können.

Format je Eintrag:

```text
## L<N>: <kurzer Pattern-Name>
- Inkrement: I<N>
- Scope: projekt-spezifisch | generisch-stack | generisch-base
- Backport-Ziel: <template-datei> oder "keiner"
- Status: offen | backported vN@<commit> | verworfen (<grund>) | projekt-intern

<1–2 Sätze: Was ist das Pattern, warum ist es relevant?>
```

---

## Pflege

- Neuer „Lessons Learned"-Block im `CHANGELOG.md` → Eintrag `## L<N>:` hier anlegen.
- Mehrere Patterns aus einem Inkrement → mehrere `## L<N>:`-Einträge.
- Scope-Werte:
  - `projekt-spezifisch` — nur für dieses Projekt relevant, kein Backport nötig.
  - `generisch-stack` — gehört in `docs_template/stacks/<stack>/CONVENTIONS.md` o.ä.
  - `generisch-base` — gehört in `docs_template/base/` (Prinzip, Basis-Convention, MANIFEST).
- Status `offen` → wird bei der nächsten Reflexion (REFLECTION.md Schritt 3) als Backport-Vorschlag gemeldet;
  bleibt `offen` bis zur Freigabe.
- Status `backported vN@<commit>` → Backport-Commit ins Template-Repo durchgeführt (Template-Stand
  Version + Commit, z. B. `v26@0a4c8f1`); der Tag `vN` folgt erst nach dem Pre-Tag-Gate (REFLECTION.md Schritt 3b).
- Status `projekt-intern` → Endzustand für `Scope: projekt-spezifisch` (kein Backport vorgesehen; die Lesson gilt
  im Projekt weiter). Wird in der Reflexion nicht als Backport-Kandidat gemeldet.
- Niemals Patterns löschen, auch wenn überholt — `Status: verworfen (<grund>)` setzen.
- Diese Datei steht in der Trigger-Matrix in `KICKOFF.md` Phase 3a.
