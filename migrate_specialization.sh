#!/bin/bash
# ============================================================
# migrate_specialization.sh
# Applique la migration EF Core "AddSpecialization" sur la base
# PostgreSQL du docker-compose PolyPresence.
#
# Usage (depuis la racine du projet) :
#   chmod +x migrate_specialization.sh
#   ./migrate_specialization.sh
#
# Prérequis :
#   - Docker Compose lancé (le conteneur postgres doit tourner)
#   - .NET SDK 8.0 installé localement OU utiliser le conteneur backend
# ============================================================

set -euo pipefail

# ---- Configuration ----
PG_CONTAINER="polypresence-postgres-1"   # Nom du conteneur postgres (docker compose ps)
PG_HOST="localhost"
PG_PORT="5433"
PG_DB="polypresence"
PG_USER="polypresence"
PG_PASS="polypresence"

CONNECTION_STRING="Host=${PG_HOST};Port=${PG_PORT};Database=${PG_DB};Username=${PG_USER};Password=${PG_PASS};SSL Mode=Disable"

echo "=== Migration Specialization ==="
echo ""

# ---- 1. Vérifier que le conteneur postgres tourne ----
echo "[1/4] Vérification du conteneur PostgreSQL..."
if ! docker ps --format '{{.Names}}' | grep -q postgres; then
    echo "  ⚠  Conteneur postgres introuvable. Démarrage..."
    docker compose up -d postgres
    echo "  Attente 5s que PostgreSQL démarre..."
    sleep 5
else
    echo "  ✓ Conteneur PostgreSQL actif."
fi

# ---- 2. Vérifier la connectivité ----
echo "[2/4] Test de connexion à PostgreSQL..."
if docker exec "$PG_CONTAINER" pg_isready -h localhost -p "$PG_PORT" -U "$PG_USER" > /dev/null 2>&1; then
    echo "  ✓ PostgreSQL répond."
else
    echo "  ✗ PostgreSQL ne répond pas. Vérifiez le conteneur."
    exit 1
fi

# ---- 3. Appliquer la migration via dotnet ef ----
echo "[3/4] Application de la migration EF Core..."
cd backend

# Surcharge temporaire de la connection string pour pointer vers localhost
export ConnectionStrings__DefaultConnection="$CONNECTION_STRING"

dotnet ef database update --verbose 2>&1 | tail -30

echo "  ✓ Migration appliquée."

# ---- 4. Vérification ----
echo "[4/4] Vérification de la table Specializations..."
RESULT=$(docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -p "$PG_PORT" -t -c \
    "SELECT count(*) FROM \"Specializations\" WHERE \"Code\" = 'INFO';" 2>/dev/null | tr -d ' ')

if [ "$RESULT" = "1" ]; then
    echo "  ✓ Filière 'Informatique' (INFO) présente dans la base."
else
    echo "  ⚠ Filière INFO non trouvée (count=$RESULT). Vérifiez la migration."
fi

echo ""
echo "=== Migration terminée ==="
echo ""
echo "Résumé des changements appliqués :"
echo "  - Table 'Specializations' créée"
echo "  - Colonne 'SpecializationId' ajoutée à Sessions, Users, IcsLinks"
echo "  - Filière 'Informatique' (CODE: INFO) insérée"
echo "  - Données existantes migrées vers la filière INFO"
echo ""
echo "Pour reconstruire le backend Docker :"
echo "  docker compose build backend"
echo "  docker compose up -d backend"
