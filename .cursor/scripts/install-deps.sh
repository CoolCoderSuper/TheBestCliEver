#!/usr/bin/env bash
set -euo pipefail

DOTNET_ROOT="${HOME}/.dotnet"
DOTNET_BIN="${DOTNET_ROOT}/dotnet"
DOTNET_CHANNEL="8.0"

if [[ ! -x "${DOTNET_BIN}" ]]; then
  echo "Installing .NET SDK ${DOTNET_CHANNEL} to ${DOTNET_ROOT}..."
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel "${DOTNET_CHANNEL}" --install-dir "${DOTNET_ROOT}"
else
  echo ".NET already installed at ${DOTNET_BIN}"
fi

PROFILE_FILE="${HOME}/.bashrc"
PATH_EXPORT='export PATH="$HOME/.dotnet:$PATH"'
if [[ -f "${PROFILE_FILE}" ]]; then
  if ! rg -q 'HOME/\.dotnet:\$PATH' "${PROFILE_FILE}"; then
    printf '\n%s\n' "${PATH_EXPORT}" >> "${PROFILE_FILE}"
  fi
else
  printf '%s\n' "${PATH_EXPORT}" > "${PROFILE_FILE}"
fi

export DOTNET_ROOT="${DOTNET_ROOT}"
export PATH="${DOTNET_ROOT}:${PATH}"

echo "Dependency install complete."
