#!/bin/sh
set -eu

api_port="${PORT:-8080}"
exec dotnet TarlaAsistani.API.dll --urls "http://0.0.0.0:${api_port}"
