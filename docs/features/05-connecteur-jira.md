# Phase 5 — Connecteur Jira Cloud

**Branche** : `phase-5-jira`

## Objectif

Ajouter Jira Cloud comme seconde source, **sans modifier le front ni le modèle `WorkItem`**, pour valider l'abstraction `ITaskSource`.

## API utilisée

Elle a été vérifiée dans la spécification OpenAPI officielle de Jira Cloud : l'ancien `/rest/api/3/search` y est marqué déprécié et « en cours de retrait ». Le connecteur utilise donc la **recherche améliorée** :

```
POST /rest/api/3/search/jql
{ "jql": "...", "fields": [...], "maxResults": 100, "nextPageToken": "..." }
→ { "issues": [...], "nextPageToken": "...", "isLast": false }
```

- **JQL bornée** :
  ```
  assignee = currentUser() AND resolution = Unresolved
  [AND project IN ("…")] ORDER BY updated DESC
  ```
  Les guillemets des noms de projets sont échappés.
- **Champs demandés** : uniquement `summary`, `status`, `project`, `priority`, `duedate` et `updated`.
- **Pagination** : le connecteur suit `nextPageToken` jusqu'à `isLast`, sans dépasser `MaxItems` (il ne demande que ce qui manque). Un jeton renvoyé deux fois arrête la pagination avec une erreur, plutôt que de boucler.
- **Authentification** : Basic `email:jeton d'API`. Ni l'un ni l'autre n'est jamais écrit dans les journaux ou les messages d'erreur.
- **Erreurs** : 401 et 403 signalent un échec d'authentification ; les autres erreurs reprennent les `errorMessages` de Jira (erreur de JQL, par exemple).

## Mapping (`JiraMapper`, pur)

| WorkItem | Jira |
|---|---|
| `Id` / `ExternalId` | `jira:{key}` / `key` (ex. `MOB-12`) |
| `Url` | `{BaseUrl}/browse/{key}` |
| `Project` | `project.name` (sinon `project.key`) |
| `Status` | `StatusMappings` par nom, **sinon catégorie Jira** : `new` → Todo, `indeterminate` → InProgress, `done` → Done |
| `NativePriority` | `PriorityMappings` par nom (Highest 1 … Lowest 5), sinon l'id numérique |
| `DueDate` | `duedate` (`YYYY-MM-DD`) |
| `UpdatedAt` | `updated` |

Par défaut, « Blocked », « On Hold » et « Bloqué » correspondent au statut Bloqué.

Les dates `updated` de Jira ont un décalage horaire **sans deux-points** (`2026-10-14T10:00:00.000+0200`), que .NET ne lit pas de façon fiable. `ParseTimestamp` ajoute les deux-points avant de lire la date.

## Sources non configurées

`ITaskSource` gagne un membre par défaut `bool IsConfigured => true` (ajout non cassant). Une source sans configuration est **ignorée** par la synchronisation et par `/api/sources`, au lieu d'apparaître en échec toutes les 5 minutes. L'endpoint de debug répond toujours 503. Azure DevOps et Jira l'implémentent à partir de leurs options.

## Configuration

`appsettings.json`, section `Sources:Jira` : `BaseUrl`, `Email`, `Projects`, `MaxItems` (1000), `StatusMappings`, `PriorityMappings`.

```bash
dotnet user-secrets --project src/TaskHub.Api set "Sources:Jira:BaseUrl" "https://<site>.atlassian.net"
dotnet user-secrets --project src/TaskHub.Api set "Sources:Jira:Email" "<email>"
dotnet user-secrets --project src/TaskHub.Api set "Sources:Jira:ApiToken" "<jeton : id.atlassian.com > Sécurité > Jetons d'API>"
```

## Tests

- **Mapper** : fixtures sur deux pages (`tests/TaskHub.Tests/Fixtures/Jira`), statut par nom ou par catégorie, priorité par nom ou par id, horodatages avec ou sans deux-points.
- **Source** :
  - pagination par jeton et contenu de la requête (JQL échappée, champs) ;
  - en-tête d'authentification et limite `MaxItems` ;
  - jeton répété, 401 sans fuite des identifiants, 400 avec `errorMessages`, absence de configuration.
- **Bout en bout** : le vrai `JiraSource` derrière la vraie API (seules les réponses HTTP sont simulées). Après `POST /api/sync`, les tickets apparaissent dans `/api/items`, `/api/sources` et `/api/items/facets` comme pour n'importe quelle source.
- **Source non configurée** : la synchronisation l'ignore.

## Statut

Le connecteur n'a pas encore été testé contre un vrai site Jira (aucun compte configuré). Avec la configuration réelle, Azure DevOps continue de se synchroniser et Jira est ignoré.
