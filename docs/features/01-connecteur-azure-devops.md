# Phase 1 — Connecteur Azure DevOps

**Branche** : `phase-1-azure-devops`

## Objectif

Récupérer, depuis Azure DevOps, les éléments de travail ouverts qui me sont assignés, et les convertir en `WorkItem`.

## Fonctionnement

`AzureDevOpsSource` (`src/TaskHub.Infrastructure/Sources/AzureDevOps`) implémente `ITaskSource` et s'appuie sur l'API REST Azure DevOps **7.1**, vérifiée dans la documentation officielle avant l'implémentation :

1. **Requête WIQL** au niveau de l'organisation, `POST {org}/_apis/wit/wiql?$top={MaxItems}` :
   ```sql
   SELECT [System.Id] FROM WorkItems
   WHERE [System.AssignedTo] = @Me
     AND [System.State] NOT IN ('Closed', 'Done', 'Removed')
     [AND [System.TeamProject] IN ('…')]   -- si des projets sont configurés
   ORDER BY [System.ChangedDate] DESC
   ```
2. **Détails par lots de 200 identifiants** (la limite de l'API) : `POST {org}/_apis/wit/workitemsbatch` avec `errorPolicy: "omit"`. Seuls les champs utiles sont demandés.
3. **Mapping** par `AzureDevOpsMapper`, une fonction pure :

| WorkItem | Champ Azure DevOps |
|---|---|
| `Title` | `System.Title` (`#id` si absent) |
| `Project` | `System.TeamProject` |
| `RawStatus` / `Status` | `System.State`, converti par le `StatusMapper` |
| `NativePriority` | `Microsoft.VSTS.Common.Priority` (1 à 4) |
| `DueDate` | `Microsoft.VSTS.Scheduling.DueDate`, sinon `TargetDate` |
| `UpdatedAt` | `System.ChangedDate` |
| `Url` | `https://dev.azure.com/{org}/{projet}/_workitems/edit/{id}` |

Le drapeau CMMI `Microsoft.VSTS.CMMI.Blocked = Yes` force le statut `Blocked`.

### Statuts : `StatusMapper`

Classe du domaine qui convertit un statut natif en `WorkItemStatus`, avec une table insensible à la casse et une valeur de repli (`Todo`). Les correspondances par défaut (New, Active, Resolved, Closed…) sont complétées ou remplacées par la configuration. Pour ce compte, deux statuts personnalisés sont classés « En cours » dans `appsettings.json` :

```json
"StatusMappings": { "To Review": "InProgress", "Ready to be tested": "InProgress" }
```

### Erreurs et sécurité

- **Authentification** : Basic avec le PAT. Le jeton n'est jamais écrit dans les journaux ni dans les messages d'erreur.
- **PAT invalide** : Azure DevOps répond **203 avec une page de connexion HTML** au lieu d'un 401. Ce cas est détecté et signalé comme un échec d'authentification.
- **Exceptions** : `SourceNotConfiguredException` (organisation ou PAT manquant) et `SourceRequestException` (erreur distante).
- **HTTP** : `HttpClient` typé via `IHttpClientFactory`, avec un délai d'expiration de 30 s.

## Configuration

`appsettings.json`, section `Sources:AzureDevOps` : `Organization`, `Projects` (vide = toute l'organisation), `MaxItems` (1000), `StatusMappings`.
Les valeurs sensibles se règlent en User Secrets :

```bash
dotnet user-secrets --project src/TaskHub.Api set "Sources:AzureDevOps:Organization" "<org>"
dotnet user-secrets --project src/TaskHub.Api set "Sources:AzureDevOps:Pat" "<PAT, portée Work Items (Read)>"
```

## Endpoint de debug

En développement uniquement, `GET /api/debug/{source}` (par exemple `/api/debug/azuredevops`) interroge la source en direct :

| Situation | Réponse |
|---|---|
| Succès | 200 avec la liste des `WorkItem` |
| Source non configurée | 503 (problem+json) |
| Erreur distante | 502 (problem+json) |
| Source inconnue | 404 |

Les enums sont sérialisés en chaînes.

## Tests

- Mapper : à partir de fixtures JSON (`tests/TaskHub.Tests/Fixtures/AzureDevOps`).
- Source : `HttpMessageHandler` simulé. Les tests vérifient la requête WIQL et le filtre de projets, l'en-tête d'authentification, le découpage en lots (450 identifiants → 200 + 200 + 50), le cas 203 HTML, une erreur 500 sans fuite du PAT, et l'absence de configuration.
- `StatusMapper` et endpoint de debug.

## Vérification réelle

`/api/debug/azuredevops` a renvoyé 21 éléments répartis sur 6 projets, avec des liens valides.
