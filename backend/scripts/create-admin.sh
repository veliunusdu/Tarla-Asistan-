#!/usr/bin/env bash
set -euo pipefail

# Usage: ./scripts/create-admin.sh <firebase-uid> [operator-name] [reason]

if [ -z "${1:-}" ]; then
  echo "Usage: $0 <firebase-uid> [operator-name] [reason]"
  exit 1
fi

FIREBASE_UID="$1"
OPERATOR="${2:-system-operator}"
REASON="${3:-Initial admin provisioning via CLI}"

cd "$(dirname "$0")/.."
dotnet run --project src/TarlaAsistani.API -- admin:create --firebase-uid "$FIREBASE_UID" --operator "$OPERATOR" --reason "$REASON"
