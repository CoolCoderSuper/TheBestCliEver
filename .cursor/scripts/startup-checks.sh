#!/usr/bin/env bash
set -euo pipefail

export DOTNET_ROOT="${HOME}/.dotnet"
export PATH="${DOTNET_ROOT}:${PATH}"

echo "Running startup checks..."
dotnet --info
echo "Startup checks completed."
