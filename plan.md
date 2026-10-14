# TaskHub : agrégateur personnel de tâches (Azure DevOps, Jira, …)

## Objectif

Application web personnelle qui centralise, dans une seule vue, tous les éléments de travail qui me sont assignés dans plusieurs outils (Azure DevOps, Jira, puis d'autres). V1 en **lecture seule** : chaque élément renvoie par lien vers l'outil d'origine.

## Stack

- **Backend** : .NET 9 (ou dernière LTS dispo), ASP.NET Core Minimal API, EF Core + SQLite, `BackgroundService` pour la synchro
- **Frontend** : React + TypeScript + Vite, TanStack Query, TanStack Table (ou liste simple), Tailwind CSS
- **Tests** : xUnit + FluentAssertions côté backend, Vitest + Testing Library côté front
- **Secrets** : .NET User Secrets en dev, Data Protection API (chiffrement au repos) pour les tokens stockés en base
- **Structure du repo** (monorepo) :

```
/src
  /TaskHub.Domain          # WorkItem, enums, interfaces (ITaskSource)
  /TaskHub.Infrastructure  # EF Core, connecteurs, sync
  /TaskHub.Api             # Minimal API, DI, config
/tests
  /TaskHub.Tests
/web                       # React + Vite
```

## Modèle de domaine

```csharp
public record WorkItem(
    string Id,                // "{source}:{externalId}"
    string Source,            // "azuredevops" | "jira"
    string ExternalId,
    string Title,
    string Url,
    string Project,
    WorkItemStatus Status,    // Todo | InProgress | Blocked | Done
    string RawStatus,         // statut natif conservé pour affichage
    int? NativePriority,
    DateOnly? DueDate,
    DateTimeOffset UpdatedAt,
    DateTimeOffset LastSyncedAt);

public interface ITaskSource
{
    string Name { get; }
    Task<IReadOnlyList<WorkItem>> FetchAssignedToMeAsync(CancellationToken ct);
}
```

Un `StatusMapper` configurable par source (fichier de config) convertit `RawStatus` vers `WorkItemStatus`.

## Phases

### Phase 0 : Squelette
- Créer la solution, les projets, `Directory.Build.props` (nullable, warnings as errors), `.editorconfig`
- Initialiser `/web` avec Vite + React + TS + Tailwind
- Proxy Vite vers l'API en dev, CORS configuré
- Ajouter un `CLAUDE.md` à la racine (conventions ci-dessous)
- **Critère** : `dotnet build`, `dotnet test` et `npm run build` passent ; `GET /api/health` répond 200

### Phase 1 : Connecteur Azure DevOps
- Implémenter `AzureDevOpsSource` : requête WIQL (`[System.AssignedTo] = @Me AND [System.State] NOT IN ('Closed','Done','Removed')`), puis récupération des détails par lots (API Work Items, max 200 ids par appel)
- Auth par PAT (config : organisation, projets, PAT via User Secrets)
- Mapping vers `WorkItem`, URL directe vers l'item
- Tests unitaires du mapping avec réponses JSON de fixture ; `HttpClient` injecté via `IHttpClientFactory` (handler mockable)
- **Critère** : un endpoint de debug `GET /api/debug/azuredevops` retourne mes vrais items

### Phase 2 : Persistance + synchro
- EF Core : table `WorkItems`, migrations, upsert par `Id`
- `SyncService` (`BackgroundService`) : toutes les 5 min (configurable), exécute toutes les `ITaskSource` en parallèle, isole les erreurs par source (une source en échec ne bloque pas les autres)
- Les items qui disparaissent de la source (fermés/réassignés) sont marqués inactifs puis purgés après N jours
- Table `SyncRuns` (source, début, fin, succès, message d'erreur) pour la visibilité
- **Critère** : après démarrage, la base se remplit seule ; une source volontairement cassée apparaît en erreur dans `SyncRuns` sans impacter les autres

### Phase 3 : API de lecture
- `GET /api/items` avec filtres (`source`, `project`, `status`, `dueBefore`, `q`), tri et pagination
- `GET /api/sources` (état de chaque source : dernière synchro, succès/erreur)
- `POST /api/sync` pour forcer une synchro manuelle
- DTOs dédiés (ne pas exposer les entités), OpenAPI activé
- Tests d'intégration avec `WebApplicationFactory` + SQLite en mémoire
- **Critère** : tests d'intégration verts, schéma OpenAPI généré

### Phase 4 : Frontend React
- Client API typé (générer depuis OpenAPI avec `openapi-typescript`)
- Vue principale : liste unifiée triée par score de priorité, groupable par source ou par projet
- Filtres (source, statut, projet, recherche texte), état conservé dans l'URL
- Chaque ligne : badge source, titre, projet, statut, échéance (rouge si dépassée), lien externe
- Bandeau d'état des sources (dernière synchro, erreurs) + bouton « Synchroniser »
- Auto-refresh via TanStack Query (refetch toutes les 60 s), états loading/erreur/vide soignés
- Thème clair/sombre
- **Critère** : parcours complet utilisable en local, tests Vitest sur les filtres et le tri

### Phase 5 : Connecteur Jira
- `JiraSource` : JQL `assignee = currentUser() AND resolution = Unresolved`, pagination (`nextPageToken` ou `startAt` selon la version de l'API Jira Cloud ; vérifier la doc actuelle)
- Auth Jira Cloud : email + API token
- Mapping des statuts via la config (`statusCategory` Jira utile comme valeur par défaut)
- **Critère** : les items Jira apparaissent dans la même vue sans modifier le front ni le modèle (valide l'abstraction)

### Phase 6 : Score de priorité et finitions
- Score unifié = pondération (échéance proche/dépassée, priorité native normalisée, statut En cours/Bloqué, ancienneté sans mise à jour) ; formule dans un service testé unitairement
- Vue « Aujourd'hui » : top N à traiter
- Option : résumé quotidien par e-mail ou notification
- Dockerfile multi-stage (API + front servi en statique) et `docker-compose.yml`
- **Critère** : `docker compose up` lance l'app complète

## Hors scope V1 (idées ensuite)

- Écriture en retour (changer un statut depuis TaskHub)
- Webhooks Azure DevOps/Jira pour du quasi temps réel
- Authentification multi-utilisateurs (OAuth/Entra ID)
- Connecteurs GitHub Issues, Todoist, Outlook Tasks

## Conventions à mettre dans `CLAUDE.md`

- Code C# en anglais, UI en français
- Nullable activé, pas de `!` sans justification, async/await partout avec `CancellationToken`
- Aucun secret dans le repo ; config via `appsettings.json` (valeurs non sensibles) + User Secrets / variables d'environnement
- Chaque connecteur : une classe, un mapper pur testable, des fixtures JSON dans `tests/Fixtures`
- Commits petits et conventionnels (`feat:`, `fix:`, `test:`…), une phase = une branche
- Avant de dire qu'une phase est terminée : build + tests backend et frontend verts
- Ne jamais logguer de tokens ni de payloads complets contenant des données sensibles

## Façon de travailler avec Claude Code

1. Lui donner ce fichier et lui demander de commencer par la **Phase 0** uniquement.
2. Valider chaque phase (critère de fin atteint) avant de passer à la suivante.
3. Pour les phases 1 et 5, vérifier la doc officielle actuelle des API (WIQL / Work Items, Jira Cloud REST) avant d'implémenter, car des endpoints de pagination évoluent.
