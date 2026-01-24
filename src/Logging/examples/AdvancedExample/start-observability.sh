#!/bin/bash
set -e

echo "🚀 Starting Femur Logging Advanced Example with Observability Stack"
echo ""

# Check if docker is available
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker Desktop."
    exit 1
fi

# Determine which command to use (prefer docker compose over legacy docker-compose)
if docker compose version &> /dev/null; then
    COMPOSE_CMD="docker compose"
elif command -v docker-compose &> /dev/null; then
    COMPOSE_CMD="docker-compose"
else
    echo "❌ Docker Compose is not available. Please install Docker Desktop or Docker Compose plugin."
    exit 1
fi

echo "📦 Building and starting services..."
echo ""

$COMPOSE_CMD up --build -d

echo ""
echo "✅ Services started successfully!"
echo ""
echo "📊 Access your observability tools:"
echo ""
echo "  📝 Seq (Logs):           http://localhost:5341"
echo "  🔍 Jaeger (Traces):      http://localhost:16686"
echo "  📈 Prometheus (Metrics): http://localhost:9090"
echo "  📊 Grafana (Dashboards): http://localhost:3000"
echo "      Username: admin"
echo "      Password: admin"
echo ""
echo "📝 View application logs:"
echo "  $COMPOSE_CMD logs -f app"
echo ""
echo "🛑 To stop all services:"
echo "  $COMPOSE_CMD down"
echo ""
echo "📖 See DOCKER.md for detailed usage instructions"
echo ""
