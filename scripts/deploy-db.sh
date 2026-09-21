#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."

if [ -f "$REPO_ROOT/.env" ]; then
  set -a
  source "$REPO_ROOT/.env"
  set +a
fi

DB_HOST="${DB_HOST:-127.0.0.1}"
DB_PORT="${DB_PORT:-3306}"
DB_NAME="${DB_NAME:-SolarOptimiser}"
DB_USER="${DB_USER:-SolarDev}"
DB_PASSWORD="${DB_PASSWORD:?Set DB_PASSWORD, or add it to .env, before running this script}"

DATABASE_DIR="$REPO_ROOT/src/SolarOptimiser.Persistence/Database"
SPROC_DIR="$DATABASE_DIR/StoredProcedures"

mysql_cmd() {
  mysql --host="$DB_HOST" --port="$DB_PORT" --user="$DB_USER" --password="$DB_PASSWORD" "$DB_NAME"
}

echo "Applying Schema.sql"
mysql_cmd < "$DATABASE_DIR/Schema.sql"

for f in "$SPROC_DIR"/*.sql; do
  echo "Applying $(basename "$f")"
  mysql_cmd < "$f"
done

echo "Done."
