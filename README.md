# PolyPresence

Application web de gestion des présences pour Polytech Lyon : les séances sont importées depuis l'emploi du temps ADE, les étudiants valident leur présence avec un code donné en cours, et le professeur signe la feuille d'émargement en ligne.

> ⚠️ **Logiciel propriétaire.** Le code est consultable, mais toute utilisation, copie, modification, redistribution ou tout déploiement est interdit sans l'autorisation écrite de l'auteur. Voir [LICENSE](LICENSE).

## Fonctionnalités

### Étudiants
- Connexion par numéro étudiant ou adresse e-mail.
- Création de compte par code de vérification envoyé sur l'adresse universitaire, et réinitialisation du mot de passe par e-mail.
- Affichage en temps réel du cours en cours (flux SSE) et validation de la présence avec le code de la séance.
- Enregistrement d'une signature personnelle.

### Délégués
- Accès au code de validation de la séance.
- Renvoi du lien de signature au professeur.

### Professeurs
- Signature de la feuille d'émargement depuis un lien personnel reçu par e-mail, sans compte. Le lien n'est valable qu'une semaine après la séance.
- Ou, au choix, depuis un espace professeur connecté qui liste les cours du jour.
- Suivi en direct des présences, changement de statut (présent, absent…) et ajout de commentaires.

### Administrateurs
- Import automatique et manuel des emplois du temps au format ICS (EDT de promo, LV1, LV2), avec aperçu avant import.
- Gestion des étudiants (y compris import Excel), des professeurs, des filières et des groupes (sous-groupes, langues).
- Gestion des séances : création, modification, affectation des professeurs, renvoi des mails.
- Export des feuilles d'émargement en PDF et envoi récapitulatif par e-mail.

## Stack technique

| Partie | Technologies |
|---|---|
| Backend | .NET 10, ASP.NET Core (API REST), Entity Framework Core, JWT, BCrypt, QuestPDF, Ical.Net |
| Base de données | PostgreSQL |
| Frontend | Vue 3, Vite, Pinia, Vue Router, Axios, signature_pad |
| Tests | xUnit (backend), Vitest (frontend) |
| Déploiement | Docker Compose, nginx, tunnel Cloudflare |

## Structure du projet

```
PolyPresence/
├── backend/                 # API ASP.NET Core
│   ├── Controllers/         # Endpoints REST
│   ├── Data/                # DbContext EF Core
│   ├── Middleware/          # Authentification, journalisation des requêtes
│   ├── Migrations/          # Migrations EF Core (appliquées au démarrage)
│   ├── Models/
│   └── Services/            # JWT, e-mails, import ICS, tâches planifiées
├── backend.Tests/           # Tests backend
├── front/                   # Application Vue 3
│   ├── src/
│   │   ├── api/             # Client Axios
│   │   ├── components/      # Pages, popups, composants
│   │   ├── router/
│   │   └── stores/          # Stores Pinia
│   └── nginx.conf           # Configuration nginx de production
├── pg_conf/                 # Configuration PostgreSQL
├── docker-compose.yaml        # Production
└── docker-compose.local.yaml  # Développement local
```

## Configuration

Copier les fichiers d'exemple, puis les compléter :

```bash
cp backend/.env.example backend/.env
cp front/.env.example front/.env
```

### Backend (`backend/.env`)

| Variable | Rôle |
|---|---|
| `JWT_SECRET_KEY` | Clé de signature des jetons JWT (**obligatoire**, longue et aléatoire) |
| `FRONTEND_URL` | URL du frontend, utilisée pour le CORS et les liens envoyés par e-mail (**obligatoire**) |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_EMAIL` | Serveur d'envoi des e-mails |
| `SMTP_FORCE_SEND` | `true` pour envoyer réellement les e-mails hors production (sinon aucun envoi réel) |
| `MAIL_SENT_HOUR`, `MAIL_SENT_MINUTE`, `MAIL_SENT_SECOND` | Heure de l'envoi récapitulatif quotidien |
| `EDT_IMPORT_TIME_HOUR`, `EDT_IMPORT_TIME_MINUTE`, `EDT_IMPORT_TIME_SECOND` | Heure de l'import automatique des emplois du temps |
| `ADMIN_BASE_STUDENT_NUMBER`, `ADMIN_BASE_PASSWORD` | Compte administrateur créé au premier démarrage si aucun admin n'existe |

La chaîne de connexion PostgreSQL est dans [backend/appsettings.json](backend/appsettings.json) (`ConnectionStrings:DefaultConnection`). Elle peut être surchargée par la variable d'environnement `ConnectionStrings__DefaultConnection`.

### Frontend (`front/.env`)

| Variable | Rôle |
|---|---|
| `VITE_API_URL` | URL de l'API. Laisser vide pour utiliser `/api` (proxy Vite en dev, nginx en prod). |
| `VITE_BASE_URL` | URL publique du frontend |
| `VITE_SUPPORT_EMAIL` | Adresse de support affichée dans l'application |

## Lancement en développement

Prérequis : Docker, Node.js 24 ou plus récent, et le SDK .NET 10 pour lancer les tests ou le backend hors Docker.

1. Démarrer le backend et PostgreSQL :
   ```bash
   docker compose -f docker-compose.local.yaml up -d --build
   ```
   L'API est publiée sur http://localhost:5000. Les migrations sont appliquées automatiquement au démarrage.

2. Démarrer le frontend :
   ```bash
   cd front
   npm install
   npm run dev
   ```
   L'application est disponible sur http://localhost:5173. Vite redirige `/api` vers le backend.

Un environnement Nix est aussi fourni ([flake.nix](flake.nix)).

## Tests

```bash
# Backend
dotnet test backend.Tests

# Frontend
cd front
npm test
```

## Déploiement

La production tourne avec [docker-compose.yaml](docker-compose.yaml) : backend, frontend servi par nginx, PostgreSQL et un tunnel Cloudflare (`TUNNEL_TOKEN` à fournir dans l'environnement). PostgreSQL n'est pas exposé hors du réseau Docker.

```bash
docker compose up -d --build
```

Avant une mise en production, penser à remplacer le mot de passe PostgreSQL par défaut.

## Migrations

```bash
cd backend
dotnet ef migrations add NomDeLaMigration
```

Les migrations en attente sont appliquées automatiquement au démarrage du backend.

## Licence

Copyright © 2025-2026 Eclairsombre. Tous droits réservés.

Ce projet n'est **pas** open source. Toute utilisation nécessite une autorisation écrite préalable de l'auteur, y compris par Polytech Lyon ou l'Université Lyon 1. Détails dans [LICENSE](LICENSE).
