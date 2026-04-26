#!/usr/bin/env bash
# ══════════════════════════════════════════════════════════════════════════════
# vault/setup-secrets.sh — Populate HashiCorp Vault with EDM production secrets
#
# Usage:
#   VAULT_ADDR=https://vault.example.com VAULT_TOKEN=<root-or-admin-token> \
#     ./vault/setup-secrets.sh
#
# Prerequisites:
#   - vault CLI installed and in PATH
#   - VAULT_ADDR and VAULT_TOKEN env vars set
#   - KV v2 secrets engine already enabled at "secret/"
#     (vault secrets enable -path=secret kv-v2)
#
# What this script does:
#   1. Applies the EDM app policy (vault/policy.hcl)
#   2. Creates a periodic token bound to the edm-app policy
#   3. Writes all required production secrets to Vault KV v2
#
# The printed token at the end should be injected into the deployment
# environment as VAULT_TOKEN for the EDM app service account.
# ══════════════════════════════════════════════════════════════════════════════
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'
info()    { echo -e "${BLUE}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[OK]${NC}    $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
error()   { echo -e "${RED}[ERROR]${NC} $*" >&2; }

# ── Preflight ─────────────────────────────────────────────────────────────────
if ! command -v vault &>/dev/null; then
  error "vault CLI not found. Install from https://developer.hashicorp.com/vault/install"
  exit 1
fi

if [[ -z "${VAULT_ADDR:-}" ]]; then
  error "VAULT_ADDR is not set. Example: export VAULT_ADDR=https://vault.example.com"
  exit 1
fi

if [[ -z "${VAULT_TOKEN:-}" ]]; then
  error "VAULT_TOKEN is not set. Use a root token or an admin token with policy-write capability."
  exit 1
fi

info "Vault address: ${VAULT_ADDR}"
vault status -format=json | python3 -c \
  "import sys,json; s=json.load(sys.stdin); print('  Initialized:', s['initialized'], '/ Sealed:', s['sealed'])" \
  2>/dev/null || warn "Could not read vault status"

# ── 1. Apply policy ───────────────────────────────────────────────────────────
info "Applying edm-app policy..."
vault policy write edm-app "${SCRIPT_DIR}/policy.hcl"
success "Policy 'edm-app' applied"

# ── 2. Create app service-account token ───────────────────────────────────────
info "Creating periodic service token for edm-app..."
APP_TOKEN=$(vault token create \
  -policy=edm-app \
  -period=720h \
  -display-name="edm-production" \
  -format=json | python3 -c "import sys,json; print(json.load(sys.stdin)['auth']['client_token'])")
success "Service token created"

# ── 3. Write secrets ──────────────────────────────────────────────────────────
info "Writing secrets to Vault KV v2..."

# Helper: prompt for a secret value if not already set in environment
prompt_secret() {
  local env_var="$1" label="$2"
  if [[ -n "${!env_var:-}" ]]; then
    echo "${!env_var}"
  else
    read -rsp "  Enter ${label}: " val
    echo
    echo "${val}"
  fi
}

# Database password
DB_PASSWORD=$(prompt_secret "EDM_DB_PASSWORD" "PostgreSQL password for edm_user")
vault kv put secret/edm/prod/db_password value="${DB_PASSWORD}"
success "secret/edm/prod/db_password written"

# JWT secret (minimum 32 chars; generate one if not provided)
if [[ -z "${EDM_JWT_SECRET:-}" ]]; then
  warn "EDM_JWT_SECRET not set — generating a random 48-byte secret"
  EDM_JWT_SECRET=$(openssl rand -base64 48 | tr -d '\n')
  echo "  Generated JWT secret (save this): ${EDM_JWT_SECRET}"
fi
vault kv put secret/edm/prod/jwt_secret value="${EDM_JWT_SECRET}"
success "secret/edm/prod/jwt_secret written"

# Webhook HMAC secret
if [[ -z "${EDM_WEBHOOK_SECRET:-}" ]]; then
  EDM_WEBHOOK_SECRET=$(openssl rand -base64 32 | tr -d '\n')
  info "Generated webhook secret (save this): ${EDM_WEBHOOK_SECRET}"
fi
vault kv put secret/edm/prod/webhook_secret value="${EDM_WEBHOOK_SECRET}"
success "secret/edm/prod/webhook_secret written"

# Seq API key (optional — may be empty)
SEQ_KEY="${EDM_SEQ_API_KEY:-}"
vault kv put secret/edm/prod/seq_api_key value="${SEQ_KEY}"
success "secret/edm/prod/seq_api_key written (${SEQ_KEY:+(set)}${SEQ_KEY:-(empty)})"

# Smoke test password
SMOKE_PASS=$(prompt_secret "EDM_SMOKE_TEST_PASSWORD" "smoke-test account password")
vault kv put secret/edm/prod/smoke_test_password value="${SMOKE_PASS}"
success "secret/edm/prod/smoke_test_password written"

# ── Summary ───────────────────────────────────────────────────────────────────
echo
echo "══════════════════════════════════════════════════════"
echo -e " ${GREEN}Vault setup complete${NC}"
echo "══════════════════════════════════════════════════════"
echo
echo "  Add the following to your deployment environment or"
echo "  inject it via your CI/CD secrets manager:"
echo
echo "    VAULT_ADDR=${VAULT_ADDR}"
echo "    VAULT_TOKEN=${APP_TOKEN}"
echo
echo "  Then in docker-compose / app startup, resolve each"
echo "  secret with 'vault kv get -field=value secret/edm/prod/<name>'."
echo
warn "Revoke the root/admin token you used for this script now that setup is done."
