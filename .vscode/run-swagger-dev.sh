#!/bin/bash
set -e
cd "$(dirname "$0")/.."
export ASPNETCORE_ENVIRONMENT=Development
PORT=5188
while lsof -nP -iTCP:${PORT} -sTCP:LISTEN >/dev/null 2>&1; do
  PORT=$((PORT + 1))
done
URL="http://127.0.0.1:${PORT}"
echo "Starting API on ${URL}"
dotnet run --no-launch-profile --project LogisticPlatform.API/LogisticPlatform.API.csproj --urls "${URL}" > /tmp/logistic-platform.log 2>&1 &
SERVER_PID=$!
trap 'kill $SERVER_PID 2>/dev/null || true' EXIT
for i in $(seq 1 60); do
  if curl -s "${URL}/swagger/index.html" >/dev/null 2>&1; then
    echo "Swagger available at: ${URL}/swagger/index.html"
    if command -v open >/dev/null 2>&1; then
      open "${URL}/swagger/index.html" >/dev/null 2>&1 || true
    fi
    break
  fi
  sleep 1
done
wait $SERVER_PID
