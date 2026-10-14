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
dotnet tool restore                   # dotnet-ef (manifeste local)
dotnet ef migrations add <Nom> --project src/TaskHub.Infrastructure --startup-project src/TaskHub.Api --output-dir Persistence/Migrations
```

## Persistance et synchro

- SQLite via EF Core (`ConnectionStrings:TaskHub`, chemin relatif à la racine de contenu : `src/TaskHub.Api/taskhub.db` en dev, ignoré par git). Migrations appliquées au démarrage.
- Les `DateTimeOffset` sont stockés en millisecondes Unix (SQLite ne sait ni comparer ni trier des `DateTimeOffset`).
- `SyncRunner` (singleton) synchronise toutes les `ITaskSource` en parallèle, un `DbContext` par source, erreurs isolées et tracées dans `SyncRuns`. Les exécutions ne se chevauchent pas. `SyncService` l'appelle au démarrage puis toutes les `Sync:IntervalMinutes`.
- Items absents de leur source : `IsActive = false` + `InactiveSince`, purgés après `Sync:InactiveRetentionDays`. Une source en échec ne désactive jamais ses items.
- Tests : `TaskHubApiFactory` (fichier SQLite jetable, synchro désactivée, PAT vidé) pour tout test qui démarre l'API, afin de ne jamais toucher la base de dev ni le réseau.

## Configuration des sources

Section `Sources:<Nom>` de `appsettings.json` (valeurs non sensibles) ; secrets en User Secrets :

```bash
dotnet user-secrets --project src/TaskHub.Api set "Sources:AzureDevOps:Organization" "<org>"
dotnet user-secrets --project src/TaskHub.Api set "Sources:AzureDevOps:Pat" "<pat Work Items (Read)>"
```

`Sources:AzureDevOps:Projects` (vide = toute l'organisation) et `StatusMappings` (s'ajoutent aux valeurs par défaut de `AzureDevOpsOptions`) sont optionnels. En Development, `GET /api/debug/{source}` interroge une source en direct (503 si non configurée, 502 si erreur distante).

## API

- `GET /api/items` (filtres multi-valués `source`, `project`, `status`, plus `dueBefore` inclusif, `q`, `sort` avec `-` pour décroissant, `page`, `pageSize` ≤ 200), `GET /api/sources`, `POST /api/sync` (409 si déjà en cours). Seuls les éléments actifs sont listés.
- Contrats publics dans `src/TaskHub.Api/Contracts` : ne jamais renvoyer d'entité ni de type de domaine directement.
- OpenAPI : `/openapi/v1.json` à l'exécution, et `web/openapi/taskhub.json` régénéré à chaque build de l'API (versionné : son diff montre les changements de contrat). Pendant cette génération, `Program.cs` ne migre pas la base et désactive la synchro.
- JSON : enums en chaînes, nombres stricts (`JsonNumberHandling.Strict`).

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
