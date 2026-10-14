# Phase 2 — Persistance et synchronisation

**Branche** : `phase-2-persistence-sync`

## Objectif

Stocker les éléments localement et les tenir à jour automatiquement, sans qu'une source en échec ne bloque les autres.

## Persistance (EF Core + SQLite)

`TaskHubDbContext` (`src/TaskHub.Infrastructure/Persistence`) contient deux tables :

**`WorkItems`** reprend les champs de `WorkItem`, plus :
- `IsActive` : passe à faux quand l'élément ne revient plus de sa source (fermé ou réassigné) ;
- `InactiveSince` : date à laquelle il est devenu inactif.

L'index est sur `(Source, IsActive)` et le statut est stocké en texte.

**`SyncRuns`** contient une ligne par tentative de synchronisation d'une source : `Source`, `StartedAt`, `FinishedAt`, `Succeeded`, `ItemCount`, `ErrorMessage`.

Points techniques :
- **Dates** : les `DateTimeOffset` sont stockés en **millisecondes Unix**, car SQLite ne sait ni comparer ni trier des `DateTimeOffset` stockés en texte.
- **Migrations** : la migration initiale est appliquée au démarrage de l'API (`InitializeTaskHubDatabaseAsync`).
- **Emplacement de la base** : `ConnectionStrings:TaskHub` (par défaut `Data Source=taskhub.db`), avec un chemin relatif à la racine de contenu. En développement, c'est `src/TaskHub.Api/taskhub.db`, ignoré par git.

## Synchronisation

### `SyncRunner` (singleton)

- **Parallélisme** : synchronise toutes les `ITaskSource` en parallèle, avec un `DbContext` par source.
- **Isolation des erreurs** : une exception dans une source est enregistrée dans `SyncRuns` sans affecter les autres. Le run est enregistré dans un contexte séparé, pour être sauvegardé même si la mise à jour des données échoue.
- **Upsert par `Id`** : ajout ou mise à jour, et réactivation d'un élément qui réapparaît.
- **Éléments disparus** : ils sont marqués inactifs, puis supprimés après `Sync:InactiveRetentionDays` jours. Une source en échec ne désactive jamais ses éléments.
- **Pas de chevauchement** : un sémaphore fait qu'un second appel attend la fin du premier.
- **Garde-fou** : une source qui renvoie des éléments d'une autre source est refusée.

### `SyncService` (BackgroundService)

- Appelle `SyncRunner` au démarrage, puis toutes les `Sync:IntervalMinutes` minutes, à l'aide d'un `PeriodicTimer` qui s'appuie sur `TimeProvider`.
- Peut être désactivé avec `Sync:Enabled = false` (c'est le cas dans les tests).

## Configuration

```json
"ConnectionStrings": { "TaskHub": "Data Source=taskhub.db" },
"Sync": { "Enabled": true, "IntervalMinutes": 5, "InactiveRetentionDays": 7 }
```

## Outillage

- `dotnet-ef` est déclaré dans un manifeste d'outils local (`dotnet tool restore`).
- Les migrations sont générées dans `src/TaskHub.Infrastructure/Persistence/Migrations` et exclues de l'analyse de code via `.editorconfig`.

## Tests

- **`SyncRunner`**, sur un fichier SQLite jetable : insertion, mise à jour, passage en inactif puis réactivation, suppression après le délai de rétention (à la minute près), source en échec isolée, source qui renvoie des éléments d'une autre source, exécutions concurrentes sérialisées.
- **Démarrage** : une source saine et une source volontairement cassée. La base se remplit seule et l'échec apparaît dans `SyncRuns`.
- **`TaskHubApiFactory`** : tout test qui démarre l'API utilise un fichier SQLite jetable, la synchronisation désactivée et un PAT vide. Les tests ne touchent donc jamais la base de développement ni le réseau.

## Vérification réelle

- **Remplissage** : au démarrage, 21 éléments ont été synchronisés en environ 1,5 s.
- **Échec isolé** : avec un PAT volontairement invalide, l'échec est enregistré dans `SyncRuns` (« authentication failed (HTTP 203) ») et les 21 éléments restent actifs.
