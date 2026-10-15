# Phase 6 — Score de priorité, vue « Aujourd'hui » et Docker

**Branche** : intégrée à `main` (commit `feat: add unified priority score, today view and Docker image (phase 6)`)

## Score de priorité unifié

`PriorityScorer` (`src/TaskHub.Domain/Scoring`) est une classe pure : « maintenant » et « aujourd'hui » lui sont passés en paramètres. Elle calcule un score de **0 à 100** à partir de quatre composantes, chacune comprise entre 0 et 1 :

| Composante | Poids par défaut | Calcul |
|---|---|---|
| Échéance | 40 | En retard = 1 ; aujourd'hui = 0,9 ; puis décroissance linéaire jusqu'à 0 à `DueSoonDays` (14 j) ; sans échéance = 0 |
| Priorité native | 30 | Normalisée par source : 1 = 1, plus basse priorité = 0 (`PriorityScales` : Azure DevOps 1–4, Jira 1–5) ; sans priorité = 0,4 |
| Statut | 20 | En cours 1 ; Bloqué 0,8 ; À faire 0,3 ; Terminé 0 |
| Ancienneté | 10 | Jours sans mise à jour / `StaleAfterDays` (30 j), plafonné à 1 ; 0 pour un élément terminé |

Le scorer renvoie aussi les **raisons** du score (`ScoreReasonKind`) : `Overdue`, `DueToday`, `DueSoon` (3 jours ou moins), `HighPriority` (priorité 1), `InProgress`, `Blocked`, `Stale` (au-delà de la moitié de `StaleAfterDays`). Le front les formule en français, par exemple « En retard de 4 j » ou « Sans mise à jour depuis 20 j ».

Comportement du score :
- **Jamais stocké** : il dépend de « maintenant » et est donc calculé à la lecture.
- **Fuseau horaire** : « aujourd'hui » suit le fuseau local du serveur (`TZ` dans Docker).

### API

- **Liste** : `GET /api/items?sort=-score` trie par score en mémoire (une liste personnelle reste petite), puis par échéance et par `Id`. Chaque élément porte désormais `score` et `scoreReasons`, quel que soit le tri.
- **Aujourd'hui** : `GET /api/items/today?count=N` renvoie `{ date, items }`, soit les N éléments ouverts (hors Terminé) au score le plus élevé. Par défaut `N = Scoring:TodayCount` (5), au maximum 50 ; une valeur hors bornes renvoie 400.

### Configuration

```json
"Scoring": {
  "DueDateWeight": 40, "PriorityWeight": 30, "StatusWeight": 20, "StalenessWeight": 10,
  "DueSoonDays": 14, "StaleAfterDays": 30,
  "PriorityScales": { "azuredevops": 4, "jira": 5 }, "DefaultPriorityScale": 4,
  "TodayCount": 5
}
```

Les poids sont relatifs : le score est ramené sur 100. Les poids négatifs et un `TodayCount` nul sont refusés dès le démarrage.

## Front

- **Tri par défaut** : « Score de priorité » (`-score`). La « Priorité native » reste disponible.
- **Badge de score** sur chaque ligne :
  - couleur selon le niveau : rouge à partir de 70, ambre à partir de 40, gris en dessous ;
  - les raisons s'affichent en info-bulle et sont lues par les lecteurs d'écran.
- **Panneau « À traiter en priorité aujourd'hui »** en tête de page : les 5 éléments les plus urgents, avec leurs raisons en pastilles. Il est masqué pendant un filtrage, pour garder l'attention sur la liste.

## Docker

```bash
cp .env.example .env    # renseigner les sources (optionnel)
docker compose up -d --build
# http://localhost:8080
```

Le `Dockerfile` est multi-stage :
1. **`node:24-alpine`** : `npm ci` puis `npm run build` du front.
2. **`dotnet/sdk:10.0`** : restauration, puis `publish` de l'API, sans `global.json` et sans génération OpenAPI (le document est déjà versionné).
3. **`dotnet/aspnet:10.0`** : l'API, avec le front dans `wwwroot`, lancée sous l'utilisateur non-root `app`. La base est `/data/taskhub.db`, sur le port 8080.

L'API sert le front :
- `UseDefaultFiles` et `UseStaticFiles` sont placés **avant** `UseRouting` ; sinon, le fallback SPA capte les fichiers JS et CSS (bug constaté, puis couvert par `StaticFrontEndTests`) ;
- le fallback SPA `index.html` exclut `/api/*` et `/openapi/*`, qui restent en 404.

`docker-compose.yml` :
- un volume `taskhub-data` pour la base, et `TZ` (Europe/Brussels par défaut) ;
- la configuration vient d'un `.env` (modèle `.env.example`, ignoré par git), passée variable par variable avec `${VAR:-}`. C'est compatible avec Compose < 2.24, et le fichier `.env` est facultatif : sans lui, les sources ne sont simplement pas configurées.

`Sources:Jira:BaseUrl` est désormais une chaîne, pour qu'une valeur vide signifie « non configuré ».

## Tests

- **`PriorityScorer`** : chaque composante, la normalisation par source, les raisons, la configurabilité des poids et un cas métier (une tâche en retard de basse priorité passe devant une tâche importante sans échéance).
- **API** : `SeededApi` fige l'horloge, ce qui donne des scores déterministes calculés à la main. Les tests couvrent le tri `score` et `-score`, la pagination combinée à un filtre, `/today` et la validation de `count`.
- **Front** : score affiché, panneau prioritaire et ses raisons, panneau masqué pendant un filtrage, formulation des raisons, niveaux de score.
- **Fichiers statiques** : fichiers servis, fallback SPA, 404 pour `/api`.

## Vérification

- **Docker** : `docker compose up` sert l'application complète, sous un utilisateur non-root, avec la base sur le volume et l'heure CEST.
- **Données réelles** : le haut du classement est occupé par des tâches « En cours » sans mise à jour depuis environ 320 jours (score 50), devant les tâches « À faire » de priorité 1 (environ 46). C'est le comportement attendu de la formule, et les poids sont ajustables.

## Non réalisé

L'option « résumé quotidien par e-mail ou notification », envisagée au départ, n'est pas implémentée.
