#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ROOT_DIR}/deploy/.env.prod"
COMPOSE_FILE="${ROOT_DIR}/deploy/docker-compose.prod.yml"

IMAGE_TAG_INPUT="${1:-}"

if [[ -f "${ENV_FILE}" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "${ENV_FILE}"
  set +a
fi

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
docker compose -f "${COMPOSE_FILE}" up -d --remove-orphans

echo "Pruning dangling images..."
docker image prune -f

echo "Deploy complete."
