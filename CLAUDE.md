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

Jira Cloud (jeton : id.atlassian.com > Sécurité > Jetons d'API) :

```bash
dotnet user-secrets --project src/TaskHub.Api set "Sources:Jira:BaseUrl" "https://<site>.atlassian.net"
dotnet user-secrets --project src/TaskHub.Api set "Sources:Jira:Email" "<email>"
dotnet user-secrets --project src/TaskHub.Api set "Sources:Jira:ApiToken" "<jeton>"
```

Une source non configurée (`ITaskSource.IsConfigured == false`) est ignorée par la synchro et par `/api/sources`.

Jira utilise `POST /rest/api/3/search/jql` (pagination `nextPageToken` ; l'ancien `/rest/api/3/search` est en cours de retrait). Statut : `StatusMappings` par nom, sinon catégorie Jira. Priorité : `PriorityMappings` par nom (Highest = 1 … Lowest = 5), sinon l'id. Les dates `updated` de Jira ont un décalage sans deux-points (`+0200`), voir `JiraMapper.ParseTimestamp`.

`Sources:AzureDevOps:Projects` (vide = toute l'organisation) et `StatusMappings` (s'ajoutent aux valeurs par défaut de `AzureDevOpsOptions`) sont optionnels. En Development, `GET /api/debug/{source}` interroge une source en direct (503 si non configurée, 502 si erreur distante).

## API

- `GET /api/items` (filtres multi-valués `source`, `project`, `status`, plus `dueBefore` inclusif, `q`, `sort` avec `-` pour décroissant, `page`, `pageSize` ≤ 200), `GET /api/sources`, `POST /api/sync` (409 si déjà en cours). Seuls les éléments actifs sont listés.
- Contrats publics dans `src/TaskHub.Api/Contracts` : ne jamais renvoyer d'entité ni de type de domaine directement.
- OpenAPI : `/openapi/v1.json` à l'exécution, et `web/openapi/taskhub.json` régénéré à chaque build de l'API (versionné : son diff montre les changements de contrat). Pendant cette génération, `Program.cs` ne migre pas la base et désactive la synchro.
- JSON : enums en chaînes, nombres stricts (`JsonNumberHandling.Strict`).

## Score de priorité

- `PriorityScorer` (domaine, pur, testé) : score 0–100 = mélange pondéré (section `Scoring`) de l'échéance (en retard = 1, aujourd'hui = 0,9, puis décroissant jusqu'à `DueSoonDays`), de la priorité native normalisée par source (`PriorityScales` : Azure DevOps 1–4, Jira 1–5), du statut (En cours 1, Bloqué 0,8, À faire 0,3) et de l'ancienneté sans mise à jour (`StaleAfterDays`). Il renvoie aussi les raisons (`ScoreReasonKind`), formulées en français par le front.
- Le score dépend de « maintenant » : calculé à la lecture, jamais stocké. `sort=-score` trie en mémoire (liste personnelle, petite). « Aujourd'hui » = fuseau local du serveur (`TZ` dans Docker).
- `GET /api/items/today` : top `Scoring:TodayCount` des éléments ouverts (hors Terminé).

## Docker

- `docker compose up -d --build` puis http://localhost:8080. Image multi-stage : build du front (node), publish de l'API (SDK .NET 10, sans `global.json` ni génération OpenAPI), runtime aspnet non-root ; le front est servi depuis `wwwroot`, avec un fallback SPA qui exclut `/api` et `/openapi`.
- Configuration via `.env` (voir `.env.example`, ignoré par git), passée variable par variable dans `docker-compose.yml` (compatible Compose < 2.24). Base SQLite dans le volume `taskhub-data` (`/data`).
- Dans `Program.cs`, `UseStaticFiles` doit rester avant `UseRouting` (sinon le fallback SPA capte les assets) ; couvert par `StaticFrontEndTests`.

## Frontend (`/web`)

- Client typé : `npm run gen:api` régénère `src/api/schema.d.ts` depuis `web/openapi/taskhub.json` (après tout changement de contrat de l'API). Appels via `src/api/client.ts` (openapi-fetch), hooks TanStack Query dans `src/api/queries.ts` (rafraîchissement toutes les 60 s).
- `openapi-typescript` déclare un peer `typescript ^5` : un `overrides` dans `package.json` le fait utiliser notre TypeScript 6.
- Filtres, tri et groupage vivent dans l'URL (`src/state/filters.ts`, pur et testé ; `useFilters` pour l'historique). Le groupage est fait côté client, le tri côté serveur. La liste est chargée d'un bloc (200 max).
- Thème clair/sombre/système (`taskhub.theme` en localStorage), appliqué avant le premier rendu par le script de `index.html`.
- Tests : `mockApi` (stub de `fetch` par route) et `renderApp` dans `src/test`.
- `.claude/launch.json` : configurations `api` et `web` pour lancer l'app.

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
- Chaque feature (phase) est décrite dans `docs/features/NN-nom.md` (objectif, fonctionnement, configuration, tests, vérification), ajouté sur sa branche et référencé dans `docs/README.md`.
