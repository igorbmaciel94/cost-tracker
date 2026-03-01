#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ROOT_DIR}/deploy/.env.prod"
COMPOSE_FILE="${ROOT_DIR}/deploy/docker-compose.prod.yml"

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "Missing ${ENV_FILE}. Create it from deploy/.env.prod.example first." >&2
  exit 1
fi

IMAGE_TAG_INPUT="${1:-}"
if [[ -n "${IMAGE_TAG_INPUT}" ]]; then
  if grep -q '^IMAGE_TAG=' "${ENV_FILE}"; then
    sed -i.bak "s/^IMAGE_TAG=.*/IMAGE_TAG=${IMAGE_TAG_INPUT}/" "${ENV_FILE}"
    rm -f "${ENV_FILE}.bak"
  else
    echo "IMAGE_TAG=${IMAGE_TAG_INPUT}" >> "${ENV_FILE}"
  fi
  echo "Using IMAGE_TAG=${IMAGE_TAG_INPUT}"
fi

set -a
# shellcheck disable=SC1090
source "${ENV_FILE}"
set +a

if [[ -z "${GHCR_USERNAME:-}" || -z "${GHCR_TOKEN:-}" ]]; then
  echo "GHCR_USERNAME and GHCR_TOKEN must be defined in ${ENV_FILE}." >&2
  exit 1
fi

echo "Logging in to GHCR..."
echo "${GHCR_TOKEN}" | docker login ghcr.io -u "${GHCR_USERNAME}" --password-stdin

echo "Pulling images..."
docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" pull

echo "Applying stack update..."
docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" up -d --remove-orphans

echo "Pruning dangling images..."
docker image prune -f

echo "Deploy complete."
