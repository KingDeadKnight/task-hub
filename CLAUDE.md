# TaskHub

Agrégateur personnel (lecture seule) des éléments de travail qui me sont assignés dans Azure DevOps, Jira, etc. Le plan complet et les phases sont dans [plan.md](plan.md).

## Structure

```
/src
  /TaskHub.Domain          # WorkItem, WorkItemStatus, ITaskSource — aucune dépendance
  /TaskHub.Infrastructure  # EF Core, connecteurs, synchro
  /TaskHub.Api             # Minimal API, DI, config (http://localhost:5180)
/tests
  /TaskHub.Tests           # xUnit v3 + FluentAssertions 7 (licence Apache, ne pas passer en v8)
  /TaskHub.Tests/Fixtures  # réponses JSON des API externes
/web                       # React + TS + Vite + Tailwind v4 (http://localhost:5173, proxy /api -> 5180)
```

.NET 10 (épinglé dans `global.json`). Propriétés communes (nullable, warnings as errors, analyzers) dans `Directory.Build.props` : ne pas les redéclarer dans les `.csproj`.

## Commandes

```bash
dotnet build                          # racine
dotnet test
dotnet run --project src/TaskHub.Api  # API sur :5180
cd web && npm run dev                 # front sur :5173
cd web && npm run build && npm test && npm run lint
```

## Conventions

- Code (C# et TS) en anglais, UI en français.
- Nullable activé, pas de `!` sans justification en commentaire ; async/await partout avec `CancellationToken` propagé.
- Aucun secret dans le repo : valeurs non sensibles dans `appsettings.json`, secrets via User Secrets (`dotnet user-secrets --project src/TaskHub.Api`) ou variables d'environnement. Tokens stockés en base chiffrés avec Data Protection.
- Chaque connecteur : une classe `ITaskSource`, un mapper pur testable, des fixtures JSON dans `tests/TaskHub.Tests/Fixtures`. `HttpClient` via `IHttpClientFactory`.
- API : DTOs dédiés, jamais les entités EF exposées.
- Ne jamais logguer de tokens ni de payloads complets contenant des données sensibles.
- Commits petits et conventionnels (`feat:`, `fix:`, `test:`, `chore:`…), une phase = une branche.
- Avant de déclarer une phase terminée : `dotnet build`, `dotnet test`, `npm run build`, `npm test` verts.
- Pour les connecteurs, vérifier la doc officielle actuelle des API (WIQL / Work Items, Jira Cloud REST) avant d'implémenter.
