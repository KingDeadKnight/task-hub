# Phase 3 — API de lecture

**Branche** : `phase-3-read-api`

## Objectif

Exposer les éléments synchronisés, l'état des sources et une synchronisation à la demande, avec un contrat stable et documenté en OpenAPI.

## Endpoints

### `GET /api/items`

Liste les éléments **actifs**, filtrés, triés et paginés.

| Paramètre | Description |
|---|---|
| `source`, `project`, `status` | Filtres multi-valués : répéter le paramètre (`?status=Todo&status=Blocked`). Les statuts sont insensibles à la casse. |
| `dueBefore` | Date `YYYY-MM-DD`, incluse |
| `q` | Recherche insensible à la casse dans le titre, l'identifiant externe et le projet ; `%` et `_` sont pris littéralement |
| `sort` | `updatedAt` (défaut), `dueDate`, `priority`, `status`, `title`, `project`, `source` ; préfixe `-` pour l'ordre décroissant (défaut `-updatedAt`) |
| `page`, `pageSize` | Page à partir de 1 ; 50 éléments par défaut, 200 au maximum |

Règles de tri :
- les éléments sans échéance ou sans priorité sont **toujours en dernier** ;
- le statut est trié dans l'ordre du flux de travail : Bloqué, En cours, À faire, Terminé ;
- l'`Id` sert de critère final, pour une pagination stable.

Réponse : `{ items, page, pageSize, totalCount, totalPages }`. Un paramètre invalide renvoie **400** (`ValidationProblem`) avec le champ concerné.

### `GET /api/sources`

`{ isSyncing, sources: [{ name, activeItemCount, lastSuccessAt, lastRun }] }`, où `lastRun` vaut `{ startedAt, finishedAt, succeeded, itemCount, error }`.

### `POST /api/sync`

Lance une synchronisation de toutes les sources et attend sa fin, puis renvoie le résultat par source. Répond **409** si une synchronisation est déjà en cours.

## Contrats

- **DTOs dédiés** (`src/TaskHub.Api/Contracts/Dtos.cs`) : aucune entité EF ni aucun type de domaine n'est exposé directement.
- **Logique de requête** dans `WorkItemQueryService` (Infrastructure) ; les endpoints se limitent à valider les paramètres et à faire le mapping.
- **JSON** : enums en chaînes, nombres stricts (`JsonNumberHandling.Strict`), ce qui évite des types `integer | string` dans le schéma.

## OpenAPI

- `/openapi/v1.json` est servi à l'exécution (`Microsoft.AspNetCore.OpenApi`). Les endpoints de debug en sont exclus.
- `web/openapi/taskhub.json` est **régénéré à chaque build de l'API** (`Microsoft.Extensions.ApiDescription.Server`) et versionné : son diff montre les changements de contrat.
- **Garde-fou** : pendant cette génération, le générateur exécute `Program.cs`. Celui-ci détecte alors le processus `GetDocument.Insider` et, dans ce cas, ne migre pas la base et désactive la synchronisation. Ce problème a été constaté puis corrigé.

## Tests

- **Données de test** : `SeededApi` (fixture partagée) sème un jeu de données connu dans un SQLite jetable.
- **Filtres** : chaque filtre, leurs combinaisons, la recherche (y compris `%` et `_`), chaque tri (valeurs manquantes en dernier), la pagination et les erreurs 400.
- **Sources** : dernier run, dernier succès et nombre d'éléments actifs.
- **Synchronisation** : résultat de `POST /api/sync`, et réponse 409 testée avec une source bloquante.
- **OpenAPI** : chemins publics présents, debug absent, enums en chaînes.

## Vérification réelle

Sur les 21 éléments réels : tri par priorité, filtres combinés, recherche, erreur 400, synchronisation manuelle et document OpenAPI ont tous fonctionné.
