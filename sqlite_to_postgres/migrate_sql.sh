#!/bin/bash
# Migration script: SQLite -> PostgreSQL (Docker)
# Usage: ./migrate_sqlite_to_postgres.sh <sqlite_db_path> <docker_container_name_or_id> [pg_user] [pg_db]
# Example: ./migrate_sqlite_to_postgres.sh ./app.db my_postgres_container polypresence polypresence

# Strategy for NULLs:
#   - Colonnes nullable dans Postgres  → émettre \N (null marker COPY par défaut)
#   - Colonnes NOT NULL dans Postgres  → COALESCE(col, '') côté SQLite

set -e

SQLITE_DB="${1}"
DOCKER_CONTAINER="${2}"
PG_USER="${3:-polypresence}"
PG_DB="${4:-polypresence}"

if [ -z "$SQLITE_DB" ] || [ -z "$DOCKER_CONTAINER" ]; then
    echo "Usage: $0 <sqlite_db_path> <docker_container_name_or_id> [pg_user] [pg_db]"
    exit 1
fi

if [ ! -f "$SQLITE_DB" ]; then
    echo "Error: SQLite database file not found: $SQLITE_DB"
    exit 1
fi

for cmd in sqlite3 docker; do
    if ! command -v "$cmd" &>/dev/null; then
        echo "Error: '$cmd' is required but not installed."
        exit 1
    fi
done

if ! docker ps --format '{{.Names}} {{.ID}}' | grep -q "$DOCKER_CONTAINER"; then
    echo "Error: Docker container '$DOCKER_CONTAINER' is not running."
    exit 1
fi

echo "=== Starting migration from SQLite to PostgreSQL (Docker) ==="
echo "Source:    $SQLITE_DB"
echo "Container: $DOCKER_CONTAINER"
echo "User/DB:   $PG_USER / $PG_DB"
echo ""
# Drop all tables in the public schema (cascade to handle dependencies)
echo "--- Dropping all existing tables in public schema ---"
docker exec -i "$DOCKER_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -v ON_ERROR_STOP=1 -c "DO $$ DECLARE r RECORD; BEGIN FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP EXECUTE 'DROP TABLE IF EXISTS public.\"' || r.tablename || '\" CASCADE;'; END LOOP; END $$;"

# Recreate schema from postgres_schema.sql
echo "--- Creating tables from schema ---"
docker exec -i "$DOCKER_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -v ON_ERROR_STOP=1 < "$(dirname "$0")/postgres_schema.sql"

echo "--- Truncating existing data (in dependency order) ---"
pg "TRUNCATE TABLE public.\"Attendances\", public.\"SessionSentToUsers\", public.\"Users\", public.\"Sessions\", public.\"MailPreferences\", public.\"Professors\", public.\"IcsLinks\" CASCADE;"

echo ""
echo "--- Migrating IcsLinks ---"
sqlite_query "SELECT Id, Year, Url FROM IcsLinks;" | \
pg_copy "\COPY public.\"IcsLinks\" (\"Id\", \"Year\", \"Url\") FROM STDIN WITH (FORMAT text);"

echo "--- Migrating MailPreferences ---"
sqlite_query "SELECT Id, EmailTo, Days, CASE WHEN Active=1 THEN 'true' ELSE 'false' END FROM MailPreferences;" | \
pg_copy "\COPY public.\"MailPreferences\" (\"Id\", \"EmailTo\", \"Days\", \"Active\") FROM STDIN WITH (FORMAT text);"

echo "--- Migrating Professors ---"
# Email NOT NULL dans Postgres -> COALESCE pour éviter les NULL
sqlite_query "SELECT Id, COALESCE(Name,''), COALESCE(Firstname,''), COALESCE(Email,'') FROM Professors;" | \
pg_copy "\COPY public.\"Professors\" (\"Id\", \"Name\", \"Firstname\", \"Email\") FROM STDIN WITH (FORMAT text);"

echo "--- Migrating Sessions ---"
# ProfSignature, ProfSignatureToken, ProfId, ProfId2, ProfSignature2, ProfSignatureToken2 : nullable -> \N
sqlite_query "SELECT
    Id,
    Date,
    -- StartTime/EndTime ne contiennent que l'heure dans SQLite -> on préfixe avec la date
    CASE WHEN StartTime LIKE '%-%' THEN StartTime ELSE substr(Date,1,10) || ' ' || StartTime END,
    CASE WHEN EndTime   LIKE '%-%' THEN EndTime   ELSE substr(Date,1,10) || ' ' || EndTime   END,
    COALESCE(Year,''),
    COALESCE(Name,''),
    COALESCE(Room,''),
    COALESCE(ValidationCode,''),
    ProfSignature,
    ProfSignatureToken,
    CASE WHEN IsSent=1 THEN 'true' ELSE 'false' END,
    CASE WHEN IsMailSent=1 THEN 'true' ELSE 'false' END,
    CASE WHEN IsMailSent2=1 THEN 'true' ELSE 'false' END,
    CASE WHEN IsMerged=1 THEN 'true' ELSE 'false' END,
    ProfId,
    ProfId2,
    ProfSignature2,
    ProfSignatureToken2,
    COALESCE(TargetGroup,'')
FROM Sessions;" | \
pg_copy "\COPY public.\"Sessions\" (\"Id\", \"Date\", \"StartTime\", \"EndTime\", \"Year\", \"Name\", \"Room\", \"ValidationCode\", \"ProfSignature\", \"ProfSignatureToken\", \"IsSent\", \"IsMailSent\", \"IsMailSent2\", \"IsMerged\", \"ProfId\", \"ProfId2\", \"ProfSignature2\", \"ProfSignatureToken2\", \"TargetGroup\") FROM STDIN WITH (FORMAT text);"

echo "--- Migrating SessionSentToUsers ---"
sqlite_query "SELECT Id, SessionId, UserId, SentAt FROM SessionSentToUsers;" | \
pg_copy "\COPY public.\"SessionSentToUsers\" (\"Id\", \"SessionId\", \"UserId\", \"SentAt\") FROM STDIN WITH (FORMAT text);"

echo "--- Migrating Users ---"
# PasswordHash, RegisterToken, RegisterTokenExpiration, MailPreferencesId : nullable -> \N
sqlite_query "SELECT
    Id,
    COALESCE(Name,''),
    COALESCE(Firstname,''),
    COALESCE(StudentNumber,''),
    COALESCE(Email,''),
    COALESCE(Year,''),
    COALESCE(Signature,''),
    CASE WHEN IsAdmin=1 THEN 'true' ELSE 'false' END,
    CASE WHEN IsDelegate=1 THEN 'true' ELSE 'false' END,
    PasswordHash,
    RegisterToken,
    -- RegisterTokenExpiration : text dans SQLite -> timestamp dans Postgres
    -- On garde \N si NULL, sinon on passe la valeur telle quelle (format ISO attendu)
    CASE WHEN RegisterTokenExpiration IS NULL THEN '\N' ELSE RegisterTokenExpiration END,
    CASE WHEN RegisterMailSent=1 THEN 'true' ELSE 'false' END,
    MailPreferencesId
FROM Users;" | \
pg_copy "\COPY public.\"Users\" (\"Id\", \"Name\", \"Firstname\", \"StudentNumber\", \"Email\", \"Year\", \"Signature\", \"IsAdmin\", \"IsDelegate\", \"PasswordHash\", \"RegisterToken\", \"RegisterTokenExpiration\", \"RegisterMailSent\", \"MailPreferencesId\") FROM STDIN WITH (FORMAT text);"

echo "--- Migrating Attendances ---"
sqlite_query "SELECT Id, SessionId, StudentId, Status, Comment FROM Attendances;" | \
pg_copy "\COPY public.\"Attendances\" (\"Id\", \"SessionId\", \"StudentId\", \"Status\", \"Comment\") FROM STDIN WITH (FORMAT text);"

echo ""
echo "--- Resetting PostgreSQL sequences ---"
for table in IcsLinks MailPreferences Professors Sessions SessionSentToUsers Users Attendances; do
    docker exec -i "$DOCKER_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -v ON_ERROR_STOP=1 \
        -c "SELECT setval(pg_get_serial_sequence('public.\"${table}\"', 'Id'), COALESCE((SELECT MAX(\"Id\") FROM public.\"${table}\"), 0) + 1, false);" 2>/dev/null || \
    echo "  (Note: no sequence found for ${table}.Id)"
done

echo ""
echo "=== Migration complete! ==="