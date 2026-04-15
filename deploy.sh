#!/bin/bash
# ─────────────────────────────────────────────────
# AI Medicare Assistant — One-command deploy
# Usage: ./deploy.sh [--build] [--down] [--logs]
# ─────────────────────────────────────────────────
set -e

COMPOSE_FILE="docker-compose.yml"

case "${1:-up}" in
  --build|build)
    echo "Building and starting containers..."
    docker compose -f $COMPOSE_FILE up -d --build
    ;;
  --down|down)
    echo "Stopping containers..."
    docker compose -f $COMPOSE_FILE down
    ;;
  --logs|logs)
    docker compose -f $COMPOSE_FILE logs -f
    ;;
  --restart|restart)
    echo "Restarting containers..."
    docker compose -f $COMPOSE_FILE down
    docker compose -f $COMPOSE_FILE up -d --build
    ;;
  up|*)
    echo "Starting containers (use --build to rebuild)..."
    docker compose -f $COMPOSE_FILE up -d
    ;;
esac

if [ "${1:-up}" != "--down" ] && [ "${1:-up}" != "down" ] && [ "${1:-up}" != "--logs" ] && [ "${1:-up}" != "logs" ]; then
  echo ""
  echo "  UI:  http://localhost:9600"
  echo "  API: http://localhost:5024"
  echo ""
fi
