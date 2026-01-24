#!/bin/bash
set -e

echo "🛑 Stopping Femur Logging Advanced Example Observability Stack"
echo ""

# Determine which command to use (prefer docker compose over legacy docker-compose)
if docker compose version &> /dev/null; then
    COMPOSE_CMD="docker compose"
elif command -v docker-compose &> /dev/null; then
    COMPOSE_CMD="docker-compose"
else
    echo "❌ Docker Compose is not available. Please install Docker Desktop or Docker Compose plugin."
    exit 1
fi

# Stop and remove containers
$COMPOSE_CMD down

echo ""
echo "✅ All services stopped and containers removed"
echo ""
echo "💡 To also remove volumes (clears all data):"
echo "  $COMPOSE_CMD down -v"
echo ""
