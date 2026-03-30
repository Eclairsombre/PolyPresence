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

BASELINE_MIGRATIONS=(
    "20250509191506_InitialCreate"
    "20250514191539_modifAttendance"
    "20250514193245_CommentNullable"
    "20251214183250_RefactoringImport"
    "20260127204532_AddProfessorTable"
)

TARGET_MIGRATION_SOFTDELETE="20260305135012_SoftDeleteUser"
TARGET_MIGRATION_SPECIALIZATION="20260327080440_AddSpecialization"

echo "=== Migration Specialization ==="
echo ""

# ---- 1. Vérifier que le conteneur postgres tourne ----
echo "[1/5] Vérification du conteneur PostgreSQL..."
if ! docker ps --format '{{.Names}}' | grep -q postgres; then
    echo "  ⚠  Conteneur postgres introuvable. Démarrage..."
    docker compose up -d postgres
    echo "  Attente 15s que PostgreSQL démarre complètement..."
    sleep 15
else
    echo "  ✓ Conteneur PostgreSQL actif."
fi

# ---- 2. Vérifier la connectivité ----
echo "[2/5] Test de connexion à PostgreSQL..."
RETRY_COUNT=0
MAX_RETRIES=30
while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    if docker exec "$PG_CONTAINER" pg_isready -h localhost -p 5433 -U "$PG_USER" > /dev/null 2>&1; then
        # pg_isready répond OK, mais attendre que l'authentification soit prête
        if PGPASSWORD="$PG_PASS" docker exec "$PG_CONTAINER" psql -h localhost -p 5433 -U "$PG_USER" -d "$PG_DB" -c "SELECT 1;" > /dev/null 2>&1; then
            echo "  ✓ PostgreSQL prêt pour l'authentification (tentative $((RETRY_COUNT+1))/$MAX_RETRIES)."
            break
        fi
    fi
    RETRY_COUNT=$((RETRY_COUNT + 1))
    if [ $RETRY_COUNT -lt $MAX_RETRIES ]; then
        echo "  ⏳ PostgreSQL en démarrage... (tentative $RETRY_COUNT/$MAX_RETRIES)"
        sleep 1
    fi
done

if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
    echo "  ✗ PostgreSQL n'a pas répondu après $MAX_RETRIES tentatives. Vérifiez le conteneur."
    docker logs --tail 20 "$PG_CONTAINER" | grep -i error || echo "  → Voir les logs : docker logs $PG_CONTAINER"
    exit 1
fi

# ---- 3. Aligner l'historique EF Core et préparer le schéma ----
echo "[3/5] Alignement de l'historique EF Core..."
PGPASSWORD="$PG_PASS" docker exec -i "$PG_CONTAINER" psql -h localhost -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -v ON_ERROR_STOP=1 -c "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" character varying(150) NOT NULL, \"ProductVersion\" character varying(32) NOT NULL, CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY (\"MigrationId\"));" > /dev/null
MIGRATION_SQL=""
for migration in "${BASELINE_MIGRATIONS[@]}"; do
    MIGRATION_SQL+="INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('$migration', '8.0.4') ON CONFLICT (\"MigrationId\") DO NOTHING;"
done

PGPASSWORD="$PG_PASS" docker exec -i "$PG_CONTAINER" psql -h localhost -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -v ON_ERROR_STOP=1 -c "$MIGRATION_SQL" > /dev/null
echo "  ✓ Migrations historiques marquées comme appliquées."

echo "  Préparation des colonnes identity pour SoftDeleteUser..."
PGPASSWORD="$PG_PASS" docker exec -i "$PG_CONTAINER" psql -h localhost -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -v ON_ERROR_STOP=1 <<'SQL' > /dev/null
ALTER TABLE "Users" ALTER COLUMN "Id" DROP IDENTITY IF EXISTS;
ALTER TABLE "SessionSentToUsers" ALTER COLUMN "Id" DROP IDENTITY IF EXISTS;
ALTER TABLE "Sessions" ALTER COLUMN "Id" DROP IDENTITY IF EXISTS;
ALTER TABLE "Professors" ALTER COLUMN "Id" DROP IDENTITY IF EXISTS;
ALTER TABLE "MailPreferences" ALTER COLUMN "Id" DROP IDENTITY IF EXISTS;
ALTER TABLE "IcsLinks" ALTER COLUMN "Id" DROP IDENTITY IF EXISTS;
ALTER TABLE "Attendances" ALTER COLUMN "Id" DROP IDENTITY IF EXISTS;
SQL
echo "  ✓ Colonnes identity préparées."

# ---- 4. Appliquer les migrations cibles via dotnet ef ----
echo "[4/5] Application des migrations EF Core cibles..."
cd backend

# Restaurer les dépendances NuGet
echo "  Restauration des dépendances NuGet..."
dotnet restore --quiet

# Surcharge temporaire de la connection string pour pointer vers localhost
export ConnectionStrings__DefaultConnection="$CONNECTION_STRING"

echo "  Application de $TARGET_MIGRATION_SOFTDELETE..."
dotnet ef database update "$TARGET_MIGRATION_SOFTDELETE" --verbose 2>&1 | tail -30

echo "  Application de $TARGET_MIGRATION_SPECIALIZATION..."
dotnet ef database update "$TARGET_MIGRATION_SPECIALIZATION" --verbose 2>&1 | tail -30

echo "  ✓ Migrations cibles appliquées."

# ---- 5. Vérification ----
echo "[5/5] Vérification de la table Specializations..."
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
echo "  - Migrations historiques marquées comme appliquées dans __EFMigrationsHistory"
echo "  - Migration $TARGET_MIGRATION_SOFTDELETE appliquée"
echo "  - Migration $TARGET_MIGRATION_SPECIALIZATION appliquée"
echo "  - Table 'Specializations' créée"
echo "  - Colonne 'SpecializationId' ajoutée à Sessions, Users, IcsLinks"
echo "  - Filière 'Informatique' (CODE: INFO) insérée"
echo "  - Données existantes migrées vers la filière INFO"
echo ""
echo "Pour reconstruire le backend Docker :"
echo "  docker compose build backend"
echo "  docker compose up -d backend"
