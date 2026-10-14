# Phase 0 — Squelette du projet

**Branche** : `main` (commit `chore: scaffold TaskHub solution (phase 0)`)

## Objectif

Poser une base de projet compilable et testable, front et back, avant toute fonctionnalité : solution .NET, application React, conventions et outillage.

## Ce qui a été mis en place

### Backend (.NET 10)

| Projet | Rôle |
|---|---|
| `src/TaskHub.Domain` | Modèle métier sans dépendance : `WorkItem`, `WorkItemStatus`, `ITaskSource` |
| `src/TaskHub.Infrastructure` | Accès aux données et connecteurs (vide à ce stade) |
| `src/TaskHub.Api` | Minimal API ASP.NET Core |
| `tests/TaskHub.Tests` | xUnit v3 + FluentAssertions 7 |

- **`global.json`** épingle le SDK .NET 10.
- **`Directory.Build.props`** centralise les réglages communs : nullable activé, avertissements traités comme des erreurs, analyseurs .NET au niveau `latest-recommended`.
- **`.editorconfig`** : namespaces « file-scoped », champs privés en `_camelCase`, `CancellationToken` obligatoire (CA2016).
- **`GET /api/health`** répond `200 {"status":"ok"}`.
- **CORS** configurable par `Cors:AllowedOrigins` (en développement : `http://localhost:5173`).
- L'API écoute sur `http://localhost:5180`.

### Modèle de domaine

```csharp
public sealed record WorkItem(
    string Id,              // "{source}:{externalId}" (WorkItem.BuildId)
    string Source, string ExternalId, string Title, string Url, string Project,
    WorkItemStatus Status,  // Todo | InProgress | Blocked | Done
    string RawStatus,       // statut natif, conservé pour l'affichage
    int? NativePriority, DateOnly? DueDate,
    DateTimeOffset UpdatedAt, DateTimeOffset LastSyncedAt);

public interface ITaskSource
{
    string Name { get; }
    Task<IReadOnlyList<WorkItem>> FetchAssignedToMeAsync(CancellationToken ct);
}
```

### Frontend (`/web`)

- React 19, TypeScript, Vite et Tailwind CSS v4 (variante `dark` pilotée par une classe).
- Le serveur de développement Vite (port 5173) redirige `/api` vers l'API (`TASKHUB_API_URL`, par défaut `http://localhost:5180`).
- Une page minimale appelle `/api/health` et affiche « API connectée » ou « API injoignable ».
- Vitest, Testing Library et jsdom pour les tests ; oxlint pour le lint.

### Projet

- `CLAUDE.md` décrit les conventions : code en anglais et interface en français, aucun secret dans le dépôt, une phase par branche, commits conventionnels.
- `.gitignore` et `.gitattributes` (fins de ligne LF).

## Vérification

- `dotnet build` et `dotnet test` passent (test d'intégration de `/api/health` via `WebApplicationFactory`).
- `npm run build`, `npm test` et `npm run lint` passent.
- `/api/health` répond 200, directement comme à travers le proxy Vite, et l'en-tête CORS est présent.

## Lancer

```bash
dotnet run --project src/TaskHub.Api
cd web && npm run dev
```
