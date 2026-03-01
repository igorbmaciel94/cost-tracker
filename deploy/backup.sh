#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ROOT_DIR}/deploy/.env.prod"

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "Missing ${ENV_FILE}." >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "${ENV_FILE}"
set +a

: "${DB_HOST:?DB_HOST is required in .env.prod}"
: "${DB_PORT:?DB_PORT is required in .env.prod}"
: "${DB_NAME:?DB_NAME is required in .env.prod}"
: "${DB_USER:?DB_USER is required in .env.prod}"
: "${DB_PASSWORD:?DB_PASSWORD is required in .env.prod}"

BACKUP_DIR="${BACKUP_DIR:-/opt/cost-tracker/backups}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
DB_SSL_MODE="${DB_SSL_MODE:-Disable}"
BACKUP_DB_HOST="${BACKUP_DB_HOST:-${DB_HOST}}"

mkdir -p "${BACKUP_DIR}"
TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
BACKUP_FILE="${BACKUP_DIR}/costtracker_${TIMESTAMP}.sql.gz"

echo "Creating backup ${BACKUP_FILE}"
docker run --rm --network host -e PGPASSWORD="${DB_PASSWORD}" postgres:16 \
  pg_dump -h "${BACKUP_DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d "${DB_NAME}" --no-owner --no-privileges --sslmode="${DB_SSL_MODE}" \
  | gzip > "${BACKUP_FILE}"

find "${BACKUP_DIR}" -type f -name 'costtracker_*.sql.gz' -mtime "+${RETENTION_DAYS}" -delete

echo "Backup completed and retention applied (${RETENTION_DAYS} days)."
