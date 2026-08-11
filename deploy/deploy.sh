#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${ROOT_DIR}/deploy/docker-compose.prod.yml"

IMAGE_TAG_INPUT="${1:-}"

ENV_FILE=""
for candidate in "${ROOT_DIR}/deploy/.env.prod" "${ROOT_DIR}/.env"; do
  if [[ -f "${candidate}" ]]; then
    ENV_FILE="${candidate}"
    break
  fi
done

if [[ -z "${ENV_FILE}" ]]; then
  echo "Missing deploy/.env.prod or .env under ${ROOT_DIR}." >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "${ENV_FILE}"
set +a

if [[ -n "${IMAGE_TAG_INPUT}" ]]; then
  export IMAGE_TAG="${IMAGE_TAG_INPUT}"
  echo "Using IMAGE_TAG=${IMAGE_TAG_INPUT}"
fi

if [[ -n "${GHCR_USERNAME:-}" && -n "${GHCR_TOKEN:-}" ]]; then
  echo "Logging in to GHCR..."
  echo "${GHCR_TOKEN}" | docker login ghcr.io -u "${GHCR_USERNAME}" --password-stdin
else
  echo "Skipping GHCR login via env; assuming the server is already logged in."
fi

echo "Pulling images..."
docker compose -f "${COMPOSE_FILE}" pull

echo "Applying stack update..."
docker compose -f "${COMPOSE_FILE}" up -d --remove-orphans --wait --wait-timeout 180

echo "Checking API health..."
curl --fail --silent --show-error "http://127.0.0.1:${APP_API_BIND_PORT:-18081}/api/health" >/dev/null

echo "Pruning dangling images..."
docker image prune -f

echo "Deploy complete and healthy."
