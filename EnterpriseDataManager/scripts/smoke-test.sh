#!/usr/bin/env bash
# ══════════════════════════════════════════════════════════════════════════════
# smoke-test.sh — API smoke test for Enterprise Data Manager
#
# Usage:
#   ./scripts/smoke-test.sh <base_url>
#   ./scripts/smoke-test.sh https://edm.example.com
#
# Reads SMOKE_TEST_EMAIL and SMOKE_TEST_PASSWORD from environment (or .env).
# Exits 0 if all tests pass, 1 if any test fails.
# ══════════════════════════════════════════════════════════════════════════════
set -euo pipefail

BASE_URL="${1:-${BASE_URL:-https://localhost}}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Load .env if available
[[ -f "${SCRIPT_DIR}/../.env" ]] && source "${SCRIPT_DIR}/../.env" 2>/dev/null || true

EMAIL="${SMOKE_TEST_EMAIL:-}"
PASSWORD="${SMOKE_TEST_PASSWORD:-}"

# ── Colour helpers ─────────────────────────────────────────────────────────
GREEN='\033[0;32m'; RED='\033[0;31m'; YELLOW='\033[1;33m'; NC='\033[0m'
PASS=0; FAIL=0

pass() { echo -e "${GREEN}  PASS${NC}  $1"; PASS=$((PASS+1)); }
fail() { echo -e "${RED}  FAIL${NC}  $1"; FAIL=$((FAIL+1)); }
section() { echo; echo "── $1 ──"; }

# ── Helper: HTTP status code check ────────────────────────────────────────
check_status() {
  local label="$1" url="$2" expected="$3"
  local extra_args=("${@:4}")
  local actual
  actual=$(curl -sk -o /dev/null -w "%{http_code}" \
    --max-time 10 \
    "${extra_args[@]}" \
    "${url}" 2>/dev/null) || actual="000"

  if [[ "${actual}" == "${expected}" ]]; then
    pass "${label}  (${actual})"
  else
    fail "${label}  (expected ${expected}, got ${actual})"
  fi
}

echo "══════════════════════════════════════════════════════"
echo " EDM Smoke Tests — ${BASE_URL}"
echo "══════════════════════════════════════════════════════"

# ── 1. Unauthenticated health endpoints ───────────────────────────────────
section "Health endpoints"
check_status "GET /health"  "${BASE_URL}/health"  "200"
check_status "GET /healthz" "${BASE_URL}/healthz" "200"

# ── 2. Unauthenticated endpoints should return 401 ────────────────────────
section "Auth enforcement"
check_status "GET /api/v1/archive-plans (no token)"  "${BASE_URL}/api/v1/archive-plans"  "401"
check_status "GET /api/v1/archive-jobs (no token)"   "${BASE_URL}/api/v1/archive-jobs"   "401"
check_status "GET /api/v1/dashboard/summary (no token)" "${BASE_URL}/api/v1/dashboard/summary" "401"

# ── 3. Login ──────────────────────────────────────────────────────────────
section "Authentication"

if [[ -z "${EMAIL}" ]] || [[ -z "${PASSWORD}" ]]; then
  echo -e "${YELLOW}  SKIP${NC}  Login — SMOKE_TEST_EMAIL / SMOKE_TEST_PASSWORD not set; skipping authenticated tests"
  TOKEN=""
else
  LOGIN_RESPONSE=$(curl -sk --max-time 10 \
    -X POST "${BASE_URL}/api/v1/auth/token" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"${EMAIL}\",\"password\":\"${PASSWORD}\"}" \
    2>/dev/null) || LOGIN_RESPONSE=""

  HTTP_CODE=$(curl -sk -o /dev/null -w "%{http_code}" --max-time 10 \
    -X POST "${BASE_URL}/api/v1/auth/token" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"${EMAIL}\",\"password\":\"${PASSWORD}\"}" \
    2>/dev/null) || HTTP_CODE="000"

  if [[ "${HTTP_CODE}" == "200" ]]; then
    pass "POST /api/v1/auth/token → 200"
    TOKEN=$(echo "${LOGIN_RESPONSE}" | python3 -c \
      "import sys,json; d=json.load(sys.stdin); print(d.get('data',{}).get('accessToken','') or d.get('accessToken',''))" \
      2>/dev/null || echo "")
    if [[ -z "${TOKEN}" ]]; then
      fail "Token extraction — could not parse accessToken from login response"
    else
      pass "Token extraction — accessToken received"
    fi
  else
    fail "POST /api/v1/auth/token  (expected 200, got ${HTTP_CODE})"
    TOKEN=""
  fi
fi

# ── 4. Authenticated API endpoints ────────────────────────────────────────
if [[ -n "${TOKEN}" ]]; then
  AUTH=(-H "Authorization: Bearer ${TOKEN}")

  section "Archive Plans"
  check_status "GET /api/v1/archive-plans"        "${BASE_URL}/api/v1/archive-plans"        "200" "${AUTH[@]}"
  check_status "GET /api/v1/archive-plans (page)" "${BASE_URL}/api/v1/archive-plans?page=1" "200" "${AUTH[@]}"

  section "Archive Jobs"
  check_status "GET /api/v1/archive-jobs"         "${BASE_URL}/api/v1/archive-jobs"         "200" "${AUTH[@]}"

  section "Recovery Jobs"
  check_status "GET /api/v1/recovery-jobs"        "${BASE_URL}/api/v1/recovery-jobs"        "200" "${AUTH[@]}"

  section "Retention Policies"
  check_status "GET /api/v1/retention-policies"   "${BASE_URL}/api/v1/retention-policies"   "200" "${AUTH[@]}"

  section "Audit Logs"
  check_status "GET /api/v1/audit-logs"           "${BASE_URL}/api/v1/audit-logs"           "200" "${AUTH[@]}"

  section "Dashboard"
  check_status "GET /api/v1/dashboard/summary"    "${BASE_URL}/api/v1/dashboard/summary"    "200" "${AUTH[@]}"
  check_status "GET /api/v1/dashboard/stats"      "${BASE_URL}/api/v1/dashboard/stats"      "200" "${AUTH[@]}"
  check_status "GET /api/v1/dashboard/health"     "${BASE_URL}/api/v1/dashboard/health"     "200" "${AUTH[@]}"
  check_status "GET /api/v1/dashboard/activity"   "${BASE_URL}/api/v1/dashboard/activity"   "200" "${AUTH[@]}"
  check_status "GET /api/v1/dashboard/storage-usage" "${BASE_URL}/api/v1/dashboard/storage-usage" "200" "${AUTH[@]}"

  section "Reports (download endpoints)"
  check_status "GET /api/v1/reports/archive-jobs/excel" \
    "${BASE_URL}/api/v1/reports/archive-jobs/excel" "200" "${AUTH[@]}"
  check_status "GET /api/v1/reports/archive-jobs/pdf" \
    "${BASE_URL}/api/v1/reports/archive-jobs/pdf"   "200" "${AUTH[@]}"
  check_status "GET /api/v1/reports/audit-log/excel" \
    "${BASE_URL}/api/v1/reports/audit-log/excel"    "200" "${AUTH[@]}"
  check_status "GET /api/v1/reports/audit-log/pdf" \
    "${BASE_URL}/api/v1/reports/audit-log/pdf"      "200" "${AUTH[@]}"

  section "Security — token replay / manipulation"
  check_status "Expired token rejected" \
    "${BASE_URL}/api/v1/archive-plans" "401" \
    -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0IiwiZXhwIjoxfQ.invalid"

  section "Rate limiter — verify 429 is possible"
  # Fire 12 rapid unauthenticated requests to the auth endpoint and expect at
  # least one 429 to confirm the rate limiter is active in production.
  RATE_LIMIT_HIT=false
  for i in $(seq 1 12); do
    CODE=$(curl -sk -o /dev/null -w "%{http_code}" --max-time 5 \
      -X POST "${BASE_URL}/api/v1/auth/token" \
      -H "Content-Type: application/json" \
      -d '{"email":"probe@example.com","password":"probe"}' 2>/dev/null) || CODE="000"
    if [[ "${CODE}" == "429" ]]; then
      RATE_LIMIT_HIT=true
      break
    fi
  done
  if [[ "${RATE_LIMIT_HIT}" == "true" ]]; then
    pass "Rate limiter is active (429 received)"
  else
    fail "Rate limiter did not trigger after 12 rapid requests — check configuration"
  fi
fi

# ── Summary ────────────────────────────────────────────────────────────────
echo
echo "══════════════════════════════════════════════════════"
echo -e " Results: ${GREEN}${PASS} passed${NC}  ${RED}${FAIL} failed${NC}"
echo "══════════════════════════════════════════════════════"

[[ "${FAIL}" -eq 0 ]]
