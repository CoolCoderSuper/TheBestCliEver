#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PORT="${PORT:-8963}"
SERVER_WORKDIR="$(mktemp -d)"
SERVER_LOG="${SERVER_WORKDIR}/server.log"
SERVER_PID=""

declare -A CLIENT_FDS=()

cleanup() {
  set +e

  for name in "${!CLIENT_FDS[@]}"; do
    close_client "${name}" >/dev/null 2>&1 || true
  done

  if [[ -n "${SERVER_PID}" ]]; then
    kill "${SERVER_PID}" >/dev/null 2>&1 || true
    wait "${SERVER_PID}" >/dev/null 2>&1 || true
  fi

  rm -rf "${SERVER_WORKDIR}"
}

trap cleanup EXIT

pass() {
  printf 'PASS: %s\n' "$1"
}

fail() {
  printf 'FAIL: %s\n' "$1" >&2
  if [[ -f "${SERVER_LOG}" ]]; then
    printf '\n--- server log ---\n' >&2
    sed -n '1,200p' "${SERVER_LOG}" >&2
  fi

  exit 1
}

close_fd() {
  local fd="$1"
  eval "exec ${fd}>&-"
  eval "exec ${fd}<&-"
}

close_client() {
  local name="$1"

  if [[ -n "${CLIENT_FDS[$name]:-}" ]]; then
    close_fd "${CLIENT_FDS[$name]}"
    unset "CLIENT_FDS[$name]"
  fi
}

wait_for_server() {
  local probe_fd=""

  for _ in $(seq 1 40); do
    if exec {probe_fd}<>"/dev/tcp/127.0.0.1/${PORT}" 2>/dev/null; then
      close_fd "${probe_fd}"
      return 0
    fi

    sleep 0.25
  done

  fail "server failed to start on port ${PORT}"
}

start_server() {
  (
    cd "${SERVER_WORKDIR}"
    dotnet "${ROOT_DIR}/Server/bin/Debug/net8.0/Server.dll" "${PORT}" >"${SERVER_LOG}" 2>&1
  ) &

  SERVER_PID=$!
  wait_for_server
  pass "server started on port ${PORT}"
}

read_line() {
  local name="$1"
  local timeout_seconds="$2"
  local __result_var="$3"
  local fd="${CLIENT_FDS[$name]:-}"
  local line=""

  if [[ -z "${fd}" ]]; then
    fail "client '${name}' is not connected"
  fi

  if IFS= read -r -t "${timeout_seconds}" -u "${fd}" line; then
    printf -v "${__result_var}" '%s' "${line}"
    return 0
  fi

  return 1
}

expect_line() {
  local name="$1"
  local timeout_seconds="$2"
  local expected="$3"
  local description="$4"
  local actual=""

  if ! read_line "${name}" "${timeout_seconds}" actual; then
    fail "${description} (timed out waiting for '${expected}')"
  fi

  if [[ "${actual}" != "${expected}" ]]; then
    fail "${description} (expected '${expected}', got '${actual}')"
  fi

  pass "${description}"
}

expect_no_line() {
  local name="$1"
  local timeout_seconds="$2"
  local description="$3"
  local actual=""

  if read_line "${name}" "${timeout_seconds}" actual; then
    fail "${description} (unexpected '${actual}')"
  fi

  pass "${description}"
}

send_line() {
  local name="$1"
  local message="$2"
  local fd="${CLIENT_FDS[$name]:-}"

  if [[ -z "${fd}" ]]; then
    fail "client '${name}' is not connected"
  fi

  printf '%s\n' "${message}" >&"${fd}"
}

open_client() {
  local name="$1"
  local channel="$2"
  local username="$3"
  local password="$4"
  local expected_auth="$5"
  local fd=""
  local auth_line=""

  exec {fd}<>"/dev/tcp/127.0.0.1/${PORT}" || fail "unable to open socket for ${name}"
  CLIENT_FDS["${name}"]="${fd}"

  printf '%s\n' "${channel}" >&"${fd}"
  printf '%s:%s\n' "${username}" "${password}" >&"${fd}"

  if ! read_line "${name}" 2 auth_line; then
    fail "timed out waiting for auth response for ${name}"
  fi

  if [[ "${auth_line}" != "${expected_auth}" ]]; then
    fail "unexpected auth response for ${name} (expected '${expected_auth}', got '${auth_line}')"
  fi

  pass "${name} authenticated with ${expected_auth}"
}

build_solution() {
  dotnet build "${ROOT_DIR}/TcpChat.sln" >/dev/null
  pass "solution built successfully"
}

verify_users_file() {
  local users_file="${SERVER_WORKDIR}/users.json"

  [[ -f "${users_file}" ]] || fail "users.json was not created"

  rg '"Username": "alice"' "${users_file}" >/dev/null || fail "users.json missing alice"
  rg '"Username": "bob"' "${users_file}" >/dev/null || fail "users.json missing bob"
  rg '"Username": "charlie"' "${users_file}" >/dev/null || fail "users.json missing charlie"

  pass "users.json persisted registered users"
}

main() {
  build_solution
  start_server

  open_client alpha general alice secret AUTH_OK
  expect_line alpha 2 "alice has joined the chat" "newly connected user receives own join message"

  open_client bravo general bob hunter2 AUTH_OK
  expect_line alpha 2 "bob has joined the chat" "existing channel member sees join presence"
  expect_line bravo 2 "bob has joined the chat" "joining user sees own join presence"

  open_client charlie random charlie sidepass AUTH_OK
  expect_line charlie 2 "charlie has joined the chat" "other channel user sees own join presence"

  open_client wrong-pass general alice wrongpass AUTH_FAIL
  close_client wrong-pass
  pass "existing user rejects wrong password"

  send_line alpha "hello general"
  expect_line alpha 2 "alice: hello general" "sender receives echoed channel message"
  expect_line bravo 2 "alice: hello general" "same-channel peer receives broadcast message"
  expect_no_line charlie 1 "other channel does not receive general-channel message"

  send_line charlie "hello random"
  expect_line charlie 2 "charlie: hello random" "sender in second channel receives own message"
  expect_no_line alpha 1 "general channel does not receive random-channel message"
  expect_no_line bravo 1 "second general client does not receive random-channel message"

  close_client bravo
  expect_line alpha 2 "bob has left the chat" "leave presence reaches remaining channel members"
  expect_no_line charlie 1 "leave presence stays within the original channel"

  close_client alpha
  open_client alpha-reconnect general alice secret AUTH_OK
  expect_line alpha-reconnect 2 "alice has joined the chat" "persisted user can reconnect with the same password"

  verify_users_file
  pass "end-to-end protocol verification completed"
}

main "$@"
