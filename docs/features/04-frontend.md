# Phase 4 — Frontend React

**Branche** : `phase-4-frontend`

## Objectif

Une vue unique et utilisable de toutes mes tâches : filtres, tri, groupage, état des sources et synchronisation manuelle.

## Client API typé

- **Types générés** : `npm run gen:api` génère `src/api/schema.d.ts` depuis `web/openapi/taskhub.json` avec `openapi-typescript`.
  - Ce paquet déclare un peer `typescript ^5` ; un `overrides` ciblé dans `package.json` lui fait utiliser le TypeScript 6 du projet.
- **Appels** : `src/api/client.ts` utilise `openapi-fetch`. Les erreurs deviennent des `ApiError` dont le message vient du `detail` ou du `title` du problem+json.
- **Hooks TanStack Query** (`src/api/queries.ts`) : `useItems`, `useFacets`, `useSources`, `useSync`.
  - Les données se rafraîchissent toutes les **60 s** ; l'état des sources est interrogé toutes les 3 s pendant une synchronisation.
  - Après une synchronisation manuelle, toutes les requêtes sont invalidées.

## Interface

- **En-tête** : titre, bouton **Synchroniser** (désactivé pendant une synchronisation, avec affichage d'un éventuel 409) et bascule de thème.
- **Bandeau des sources** :
  - pour chaque source, nombre d'éléments et dernière synchronisation en temps relatif (« il y a 3 min ») ;
  - en cas d'échec : le message d'erreur et le dernier succès ;
  - un run en cours s'affiche « synchronisation en cours… », et non comme un échec.
- **Filtres** :
  - recherche avec un délai de 300 ms ;
  - pastilles de statut avec leurs nombres, et pastilles de source dès qu'il y a plus d'une source ;
  - sélecteur de projets à cocher ;
  - tri et groupage (aucun, par source, par projet) ;
  - bouton « Réinitialiser les filtres ».
- **Liste** : chaque ligne affiche
  - le badge de la source et le titre, en lien externe (`target="_blank"`, `rel="noopener noreferrer"`) ;
  - le projet, le numéro, la priorité et le statut natif ;
  - le badge de statut et l'échéance, **en rouge avec « En retard »** si elle est dépassée.
- **États** : squelette pendant le chargement, erreur avec bouton « Réessayer », et deux états vides distincts (« Aucune tâche ne vous est assignée » ou « Aucune tâche ne correspond à ces filtres »).
- **Thème** : clair, sombre ou système, mémorisé dans `localStorage` (`taskhub.theme`) et appliqué avant le premier rendu par un script de `index.html`.
- **Mobile** : la ligne passe en grille (badges en haut, titre sur toute la largeur), sans défilement horizontal.

## État dans l'URL

`src/state/filters.ts` (pur et testé) lit et écrit `source`, `project`, `status`, `q`, `sort` et `group` dans la query string, en omettant les valeurs par défaut. `useFilters` s'occupe de l'historique : `pushState` pour un changement de filtre, `replaceState` pendant la frappe, et écoute de `popstate`. On peut donc mettre une vue en favori et utiliser le bouton Précédent.

Le tri se fait côté serveur et le groupage côté client (en conservant l'ordre du serveur dans chaque groupe). La liste est chargée d'un bloc, 200 éléments au maximum.

## Ajout côté API

`GET /api/items/facets` renvoie les valeurs distinctes des éléments actifs (sources, projets, statuts) avec leurs nombres, pour alimenter les filtres indépendamment de la page affichée.

## Tests (Vitest + Testing Library)

- **Outils** : `mockApi` simule `fetch` route par route, et `renderApp` monte l'application avec un `QueryClient` neuf.
- **Fonctions pures** : lecture et écriture de l'URL, paramètres envoyés à l'API, groupage, dates (retard, fuseau horaire, temps relatif).
- **Parcours complets** :
  - ordre de la liste et lien externe, échéance en retard en rouge ;
  - filtres de statut, de source et de projet reflétés dans l'URL, et URL initiale appliquée au chargement ;
  - tri, recherche (une seule requête pour un mot entier), groupage ;
  - états vides, erreur puis « Réessayer », synchronisation, 409, synchronisation en cours.

## Lancer

```bash
dotnet run --project src/TaskHub.Api
npm --prefix web run dev   # http://localhost:5173
```

`.claude/launch.json` contient les configurations `api` et `web`.
