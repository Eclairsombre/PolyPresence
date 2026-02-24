#!/bin/bash
set -e

############################################
# 1. Exporter toutes les tables SQLite en CSV
############################################
echo "Export des tables SQLite en CSV..."

DB_PATH="$(dirname "$0")/backend/polytechpresence.db"
EXPORT_DIR="$(dirname "$0")/sqlite_export"
mkdir -p "$EXPORT_DIR"

TABLES=$(sqlite3 "$DB_PATH" ".tables")

for TABLE in $TABLES; do
    echo "Export de $TABLE..."
    sqlite3 -header -csv "$DB_PATH" "SELECT * FROM $TABLE;" > "$EXPORT_DIR/$TABLE.csv"
done

echo "Export terminé. Les fichiers CSV sont dans $EXPORT_DIR."

############################################
# 2. Démarrer Docker
############################################
echo "Démarrage des services Docker..."
docker compose up -d postgres backend

############################################
# 3. Attendre PostgreSQL
############################################
echo "Attente du démarrage de PostgreSQL..."
until docker compose exec -T postgres pg_isready -U polypresence; do
  echo "En attente de PostgreSQL..."
  sleep 2
done

############################################
# 4. Vider les tables applicatives
############################################
echo "Vidage des tables applicatives..."

TRUNCATE_SQL="$EXPORT_DIR/truncate_all.sql"
rm -f "$TRUNCATE_SQL"

for TABLE in $TABLES; do
  if [[ "$TABLE" == "__EFMigrationsHistory" || "$TABLE" == "__EFMigrationsLock" ]]; then
    continue
  fi
  echo "TRUNCATE TABLE \"$TABLE\" RESTART IDENTITY CASCADE;" >> "$TRUNCATE_SQL"
done

docker compose cp "$TRUNCATE_SQL" postgres:/tmp/truncate_all.sql
docker compose exec -T postgres psql -U polypresence -d polypresence -f /tmp/truncate_all.sql
docker compose exec -T postgres rm /tmp/truncate_all.sql

echo "Vidage terminé."

############################################
# 5. Import CSV → PostgreSQL
############################################
echo "Import des CSV dans PostgreSQL..."

for TABLE in $TABLES; do
  if [[ "$TABLE" == "__EFMigrationsHistory" || "$TABLE" == "__EFMigrationsLock" ]]; then
    echo "Ignore la table $TABLE (système EF Core)"
    continue
  fi

  echo "Import de $TABLE..."

  docker compose cp "$EXPORT_DIR/$TABLE.csv" postgres:/tmp/$TABLE.csv

  docker compose exec -T postgres psql -U polypresence -d polypresence -c \
    "\copy \"$TABLE\" FROM '/tmp/$TABLE.csv' DELIMITER ',' CSV HEADER;"

  docker compose exec -T postgres rm /tmp/$TABLE.csv

  echo "Import de $TABLE terminé."
done

############################################
# 6. Corrections des colonnes (SQLite → Postgres)
############################################
echo "Correction des colonnes..."

################ USERS ################
docker compose exec -T postgres psql -U polypresence -d polypresence <<'SQL'
ALTER TABLE "Users"
  ALTER COLUMN "IsAdmin" DROP DEFAULT,
  ALTER COLUMN "IsAdmin" TYPE boolean USING ("IsAdmin"::int = 1),
  ALTER COLUMN "IsAdmin" SET DEFAULT false;

ALTER TABLE "Users"
  ALTER COLUMN "IsDelegate" DROP DEFAULT,
  ALTER COLUMN "IsDelegate" TYPE boolean USING ("IsDelegate"::int = 1),
  ALTER COLUMN "IsDelegate" SET DEFAULT false;

ALTER TABLE "Users"
  ALTER COLUMN "RegisterMailSent" DROP DEFAULT,
  ALTER COLUMN "RegisterMailSent" TYPE boolean USING ("RegisterMailSent"::int = 1),
  ALTER COLUMN "RegisterMailSent" SET DEFAULT false;

ALTER TABLE "Users"
  ALTER COLUMN "RegisterTokenExpiration" TYPE timestamp USING "RegisterTokenExpiration"::timestamp;
SQL

################ SESSIONS ################
docker compose exec -T postgres psql -U polypresence -d polypresence <<'SQL'
ALTER TABLE "Sessions"
  ALTER COLUMN "Date" TYPE timestamp USING "Date"::timestamp,
  ALTER COLUMN "StartTime" TYPE interval USING "StartTime"::interval,
  ALTER COLUMN "EndTime" TYPE interval USING "EndTime"::interval,
  ALTER COLUMN "IsSent" DROP DEFAULT,
  ALTER COLUMN "IsSent" TYPE boolean USING ("IsSent"::int = 1),
  ALTER COLUMN "IsSent" SET DEFAULT false,
  ALTER COLUMN "IsMailSent" DROP DEFAULT,
  ALTER COLUMN "IsMailSent" TYPE boolean USING ("IsMailSent"::int = 1),
  ALTER COLUMN "IsMailSent" SET DEFAULT false,
  ALTER COLUMN "IsMailSent2" DROP DEFAULT,
  ALTER COLUMN "IsMailSent2" TYPE boolean USING ("IsMailSent2"::int = 1),
  ALTER COLUMN "IsMailSent2" SET DEFAULT false,
  ALTER COLUMN "IsMerged" DROP DEFAULT,
  ALTER COLUMN "IsMerged" TYPE boolean USING ("IsMerged"::int = 1),
  ALTER COLUMN "IsMerged" SET DEFAULT false;
SQL

################ SessionSentToUsers ################
docker compose exec -T postgres psql -U polypresence -d polypresence <<'SQL'
ALTER TABLE "SessionSentToUsers"
  ALTER COLUMN "SentAt" TYPE timestamp USING "SentAt"::timestamp;
SQL

################ MailPreferences ################
docker compose exec -T postgres psql -U polypresence -d polypresence <<'SQL'
ALTER TABLE "MailPreferences"
  ALTER COLUMN "Active" DROP DEFAULT,
  ALTER COLUMN "Active" TYPE boolean USING ("Active"::int = 1),
  ALTER COLUMN "Active" SET DEFAULT false;
SQL

echo "Correction des colonnes terminée."

############################################
# FIN
############################################
echo "✅ Migration terminée avec succès !"