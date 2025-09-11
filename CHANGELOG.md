# Changelog

Tous les changements notables apportés au projet PolyPresence seront documentés dans ce fichier.

## [1.0.0] - 2025-09-01

### Fonctionnalités principales
- **Gestion des sessions de cours**
  - Création et planification de sessions
  - Affichage des sessions par date, professeur ou groupe
  - Modification et suppression de sessions

- **Gestion des présences**
  - Enregistrement des présences/absences des étudiants
  - Système de code de validation pour confirmer la présence
  - Interface pour les professeurs pour marquer les présences
  - Commentaires sur les présences individuelles

- **Authentification et sécurité**
  - Connexion sécurisée pour les administrateurs et professeurs
  - Système de jetons d'accès (tokens) pour les professeurs
  - Protection des données personnelles

- **Gestion des utilisateurs**
  - Enregistrement des étudiants avec leur numéro étudiant
  - Gestion des professeurs
  - Attribution des groupes aux étudiants
  - Importation en masse des étudiants (via Excel)

- **Interface utilisateur**
  - Design responsive pour ordinateurs et appareils mobiles
  - Tableau de bord administrateur
  - Interface de signature pour les professeurs
  - Page de visualisation des présences

- **Fonctionnalités techniques**
  - Base de données SQLite pour le stockage
  - API REST backend en .NET
  - Frontend Vue.js
  - Containerisation avec Docker
  - Synchronisation avec le calendrier (ICS)

- **Rapports et statistiques**
  - Génération de feuilles de présence
  - Exportation de données

### Notes techniques
- Architecture Backend: .NET 9.0 avec Entity Framework Core
- Architecture Frontend: Vue.js 3 avec Vite
- Base de données: SQLite avec migrations
