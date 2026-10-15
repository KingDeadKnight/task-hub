# Documentation TaskHub

TaskHub agrège, en lecture seule, les éléments de travail qui me sont assignés dans plusieurs outils (Azure DevOps, Jira). Le plan d'origine est dans [plan.md](../plan.md) et les conventions de développement dans [AGENTS.md](../AGENTS.md).

## Fonctionnalités, phase par phase

| Phase | Fonctionnalité | Branche |
|---|---|---|
| 0 | [Squelette du projet](features/00-squelette.md) | `main` |
| 1 | [Connecteur Azure DevOps](features/01-connecteur-azure-devops.md) | `phase-1-azure-devops` |
| 2 | [Persistance et synchronisation](features/02-persistance-synchro.md) | `phase-2-persistence-sync` |
| 3 | [API de lecture](features/03-api-lecture.md) | `phase-3-read-api` |
| 4 | [Frontend React](features/04-frontend.md) | `phase-4-frontend` |
| 5 | [Connecteur Jira Cloud](features/05-connecteur-jira.md) | `phase-5-jira` |
| 6 | [Score de priorité, vue « Aujourd'hui » et Docker](features/06-score-priorite-docker.md) | `main` |

Chaque document présente l'objectif de la phase, ce qui a été implémenté, la configuration, les tests et la vérification réelle. Il décrit l'état du code **à la fin de la phase** : les phases suivantes ont pu faire évoluer certains points, signalés dans leur propre document.

## Prochaines étapes

[Améliorer le cycle de développement (SDLC)](sdlc/README.md) : la feuille de route à cocher (CI, protection de `main`, sécurité, tests de bout en bout, livraison, exploitation).

## Démarrage rapide

```bash
# Développement
dotnet run --project src/TaskHub.Api      # API : http://localhost:5180
npm --prefix web run dev                  # front : http://localhost:5173

# Conteneur
docker compose up -d --build              # http://localhost:8080
```
