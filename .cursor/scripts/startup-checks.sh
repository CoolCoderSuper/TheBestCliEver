#!/usr/bin/env bash
set -euo pipefail

export DOTNET_ROOT="${HOME}/.dotnet"
export PATH="${DOTNET_ROOT}:${PATH}"

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

echo "Running startup checks..."
dotnet --info

for tool in ffmpeg xdotool; do
  if command -v "${tool}" >/dev/null 2>&1; then
    echo "Found ${tool}: $(command -v "${tool}")"
  else
    echo "Warning: ${tool} is not available; desktop demo automation may be limited."
  fi
done

if [[ -f "${ROOT_DIR}/TcpChat.sln" ]]; then
  echo "Building solution..."
  dotnet build "${ROOT_DIR}/TcpChat.sln" --nologo
fi

echo "Startup checks completed."
