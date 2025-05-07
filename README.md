# PolyPresence

PolyPresence est une application de gestion de présence pour Polytech Lyon, composée d’un backend .NET (C#) et d’un frontend en Vue.

## Fonctionnalités principales
- Gestion des utilisateurs et des présences
- Gestion et utilisation de liens ICS (calendrier)
- Gestion d'envoi de mail
- Export PDF (QuestPDF, iText)
- API RESTful (ASP.NET Core)
- Base de données SQLite

## Structure du projet

```
PolyPresence/
│
├── backend/         # Backend .NET (API, services, modèles, migrations)
│   ├── Controllers/
│   ├── Data/
│   ├── Models/
│   ├── Services/
│   ├── Migrations/
│   └── ...
│
└── front/           # Frontend (Vite.js, React ou autre)
    ├── src/
    ├── public/
    └── ...
```

## Prérequis
- .NET 9 SDK
- Node.js (pour le frontend)
- SQLite (inclus)

## Configuration des fichiers .env

### Exemple de `backend/.env`
```
SMTP_USERNAME=
SMTP_PASSWORD=
SMTP_FROM_EMAIL=
FRONTEND_URL=
SMTP_HOST=
SMTP_PORT=
STORAGE_PATH=
```

### Exemple de `front/.env`
```
VITE_API_URL=
VITE_BASE_URL=
VITE_COOKIE_SECRET=
```


## Installation et lancement

### Backend
```bash
cd backend
# Vérifiez/complétez le fichier .env
# Restaurez et lancez le projet
 dotnet restore
 dotnet build
 dotnet run
```

### Frontend
```bash
cd front
# Vérifiez/complétez le fichier .env
npm install
npm run dev
```

## Accès
- Frontend : http://localhost:5173
- Backend : http://localhost:5020/api


## Licence
Projet académique – usage interne Polytech.

