# Améliorer le cycle de développement (SDLC)

Feuille de route pour fiabiliser et accélérer le développement de TaskHub. Les actions sont classées par rendement décroissant. On coche au fur et à mesure et on note les décisions en bas de page.

## État des lieux (14/10/2026)

- **Aucune CI** : le dépôt n'a pas de dossier `.github/`. Builds, tests (127 côté back, 34 côté front), lint et contrat OpenAPI ne sont vérifiés qu'à la main.
- **`main` sans protection** : on y pousse directement. Les phases ont été développées sur des branches empilées puis fusionnées hors PR.
- **Garde-fous manuels** : la cohérence entre l'API et le client TypeScript (`web/openapi/taskhub.json`, `web/src/api/schema.d.ts`) et la présence des migrations EF reposent sur la discipline.
- **Signature GPG bloquante** : les commits sont signés, mais l'expiration du cache de `gpg-agent` a interrompu le travail plusieurs fois.
- **Tests de bout en bout absents** : la page blanche de l'image Docker (assets servis en HTML) n'a été vue qu'à la vérification manuelle.

## 1. Intégration continue — priorité absolue

Workflow GitHub Actions déclenché sur chaque PR et chaque push sur `main`.

- [x] **Back** : `dotnet build` puis `dotnet test` (les analyseurs sont déjà en warnings-as-errors).
- [x] **Style C#** : `dotnet format --verify-no-changes`.
- [x] **Front** : `npm ci`, `npm run lint`, `npm run build`, `npm test` dans `web/`.
- [x] **Contrat API** : après le build de l'API, `git diff --exit-code web/openapi/taskhub.json`, puis `npm run gen:api` et `git diff --exit-code web/src/api/schema.d.ts`.
- [x] **Migrations EF** : `dotnet ef migrations has-pending-model-changes` doit être vide.
- [x] **Docker** : `docker build .` (sans push) pour valider l'image multi-stage.
- [x] **Couverture** : publier le rapport coverlet en artefact, pour suivre son évolution sans seuil arbitraire.

> Fait : `.github/workflows/ci.yml`, jobs `back`, `api-contract`, `ef-migrations`, `front` et `docker`, checks obligatoires de `main`.

## 2. Protéger `main` et passer par des PR

- [x] Règles de protection de `main` (ruleset) : CI verte obligatoire, pas de push direct ni de force push, PR réservées aux collaborateurs. Commits signés non exigés.
- [x] Une PR par feature au lieu de branches empilées : la revue, la CI et l'historique au même endroit.
- [ ] Modèle de PR (`.github/pull_request_template.md`) : description, tests ajoutés, doc `docs/features/NN-*.md` à jour, captures si l'interface change.

## 3. Dépendances et sécurité

- [ ] **Dependabot** (`.github/dependabot.yml`) : NuGet, npm (`/web`), images Docker et GitHub Actions, avec des mises à jour hebdomadaires groupées.
- [ ] **CodeQL** pour C# et TypeScript.
- [ ] **Secrets** : activer le secret scanning et le push protection de GitHub (PAT Azure DevOps dans les User Secrets, `.env` local).
- [ ] **Image Docker** : scan des vulnérabilités (Trivy) dans la CI.

## 4. Tests manquants

- [ ] **Bout en bout Playwright** contre l'image Docker : chargement de la page, filtre, synchronisation. C'est la couche qui aurait attrapé la page blanche.
- [ ] **Tests de contrat des connecteurs** : un job manuel ou planifié qui appelle le vrai Azure DevOps (et Jira) avec des secrets GitHub, pour détecter une évolution des API externes (comme la dépréciation de `/rest/api/3/search`).

## 5. Livraison

- [x] **Versions** : `release-please`, qui tire parti des commits conventionnels pour générer un changelog et une version SemVer à chaque release.
- [x] **Registre** : publier l'image sur GitHub Container Registry, étiquetée par version et par SHA, et faire pointer `docker-compose.yml` sur une image versionnée plutôt que sur `build: .`.
> Fait : `.github/workflows/release.yml` (release-please, puis publication de l'image sur GHCR au merge de la PR de release), `docker-compose.build.yml` pour construire en local. Prérequis côté dépôt (réglages à faire à la main) : Settings > Actions > General > « Allow GitHub Actions to create and approve pull requests », et un secret `RELEASE_PLEASE_TOKEN` (PAT avec écriture sur contenu et PR) pour que la CI tourne sur la PR de release, faute de quoi les checks obligatoires de `main` la bloquent. Le premier paquet GHCR est privé : le rendre public ou s'authentifier pour le tirer.

- [ ] **Déploiement** : si l'app doit tourner en permanence (NAS, VM), déploiement automatique de l'image étiquetée, ou Watchtower.

## 6. Exploitation

- [x] **Santé** : `/api/health` vérifie aussi la base et l'âge de la dernière synchro réussie, et sert de `healthcheck` dans `docker-compose.yml`.
- [x] **Logs** : journaux structurés en JSON en production. OpenTelemetry reste optionnel pour une app personnelle.
> Fait : `GET /api/health` (base + âge de la synchro, 503 sinon), `healthcheck` du compose via `dotnet TaskHub.Api.dll --healthcheck`, logs JSON hors Development.

- [ ] **Sauvegarde** : export périodique du volume `taskhub-data`. La synchro reconstruit les éléments, mais l'historique des `SyncRuns` serait perdu.

## 7. Expérience développeur

- [ ] **Lancement en une commande** : `dotnet watch` pour l'API et Vite pour le front, ensemble.

## Hors SDLC, en attente

- [ ] **Résumé quotidien par e-mail** : option de la Phase 6 non implémentée (une session a été proposée).
- [ ] **Étalonnage du score** : les tâches « En cours » sans mise à jour depuis longtemps passent devant les priorités 1 « À faire ». Ajuster `Scoring:PriorityWeight` ou `Scoring:StalenessWeight` si ce n'est pas le comportement voulu.
- [ ] **Jira réel** : tester le connecteur contre un vrai site dès qu'un compte est disponible.

## Journal des décisions

| Date | Décision |
|---|---|
| 15/10/2026 | CI GitHub Actions : un job par contrôle (`back`, `api-contract`, `ef-migrations`, `front`, `docker`), exigés par le ruleset de `main`. Couverture coverlet en artefact, sans seuil. Nommage C# : `s_camelCase` pour les champs statiques privés, PascalCase pour les constantes privées, `_camelCase` pour les autres champs privés. |
| 15/10/2026 | Ruleset de `main` : checks de CI obligatoires, push direct et force push interdits, PR réservées aux collaborateurs. La signature des commits n'est pas exigée : elle n'est pas configurée sur tous les postes et n'apporte rien de plus pour un dépôt personnel. |
| 16/10/2026 | Santé : `/api/health` répond 503 si la base échoue ou si aucune synchro n'a réussi depuis 3 intervalles (sans effet sans source configurée ou synchro désactivée) ; sonde de conteneur par `TaskHub.Api.dll --healthcheck` faute de curl dans l'image. Logs : JSON de la console hors Development, sans OpenTelemetry. Livraison : release-please (type `simple`, tag `X.Y.Z`) et publication GHCR dans le même workflow, car un release créé par `GITHUB_TOKEN` ne déclenche pas d'autre workflow. Le compose tire l'image versionnée ; la construction locale passe par `docker-compose.build.yml`. |
