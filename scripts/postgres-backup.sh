#!/usr/bin/env sh
set -eu

backup_dir="${BACKUP_DIR:-/backups}"
retention_days="${BACKUP_RETENTION_DAYS:-14}"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_file="${backup_dir}/tarla_asistani_${timestamp}.dump"

mkdir -p "$backup_dir"
pg_dump --format=custom --no-owner --no-acl "$DATABASE_URL" --file "$backup_file"
find "$backup_dir" -type f -name 'tarla_asistani_*.dump' -mtime "+$retention_days" -delete
printf '%s\n' "$backup_file"
