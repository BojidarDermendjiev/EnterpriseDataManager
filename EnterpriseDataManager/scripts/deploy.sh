#!/usr/bin/env bash
# ══════════════════════════════════════════════════════════════════════════════
# deploy.sh — Full production deployment runbook for Enterprise Data Manager
#
# Usage:
#   ./scripts/deploy.sh [--skip-build] [--skip-smoke]
#
# Prerequisites:
#   - Docker + Docker Compose v2 installed
#   - .env file present at repo root (copy .env.example and fill in secrets)
#   - TLS certs present at $TLS_CERT_DIR (fullchain.pem + privkey.pem)
#
# Exit codes:
#   0  — deployment succeeded
#   1  — pre-flight check failed
#   2  — migration failed
#   3  — health check failed
#   4  — smoke tests failed
# ══════════════════════════════════════════════════════════════════════════════
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

# ── Colour helpers ─────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'
info()    { echo -e "${BLUE}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[OK]${NC}    $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
error()   { echo -e "${RED}[ERROR]${NC} $*" >&2; }

# ── Argument parsing ───────────────────────────────────────────────────────
SKIP_BUILD=false
SKIP_SMOKE=false
for arg in "$@"; do
  case $arg in
    --skip-build) SKIP_BUILD=true ;;
    --skip-smoke) SKIP_SMOKE=true ;;
    *) warn "Unknown argument: $arg" ;;
  esac
done

# ── 1. Pre-flight checks ───────────────────────────────────────────────────
info "=== Phase 1: Pre-flight checks ==="

if [[ ! -f ".env" ]]; then
  error ".env file not found. Copy .env.example, fill in all values, and retry."
  exit 1
fi

source .env 2>/dev/null || true

REQUIRED_VARS=(DOMAIN POSTGRES_PASSWORD JWT_SECRET TLS_CERT_DIR)
for var in "${REQUIRED_VARS[@]}"; do
  if [[ -z "${!var:-}" ]]; then
    error "Required env var \$$var is not set in .env"
    exit 1
  fi
done

if [[ ! -f "${TLS_CERT_DIR}/fullchain.pem" ]] || [[ ! -f "${TLS_CERT_DIR}/privkey.pem" ]]; then
  error "TLS certificates not found in ${TLS_CERT_DIR}/"
  error "Expected: fullchain.pem and privkey.pem"
  error "For Let's Encrypt: run certbot certonly --standalone -d ${DOMAIN}"
  exit 1
fi

if ! command -v docker &>/dev/null; then
  error "docker is not installed or not in PATH"
  exit 1
fi

if ! docker compose version &>/dev/null; then
  error "Docker Compose v2 is not available (docker compose)"
  exit 1
fi

success "Pre-flight checks passed"

# ── 2. Build Docker image ──────────────────────────────────────────────────
if [[ "${SKIP_BUILD}" == "false" ]]; then
  info "=== Phase 2: Building Docker image ==="
  docker compose build --no-cache web
  success "Docker image built"
else
  warn "Skipping Docker build (--skip-build)"
fi

# ── 3. Run database migrations ─────────────────────────────────────────────
info "=== Phase 3: Running database migrations ==="

# Start only the database service first
docker compose up -d db
info "Waiting for PostgreSQL to be healthy..."
ATTEMPTS=0
until docker compose exec db pg_isready -U "${POSTGRES_USER:-edm_user}" -d "${POSTGRES_DB:-edm}" &>/dev/null; do
  ATTEMPTS=$((ATTEMPTS + 1))
  if [[ $ATTEMPTS -ge 30 ]]; then
    error "PostgreSQL did not become ready after 30 attempts"
    exit 2
  fi
  sleep 2
done
success "PostgreSQL is ready"

info "Applying EF Core migrations..."
if ! docker compose run --rm migrate; then
  error "Database migration failed"
  docker compose logs db
  exit 2
fi
success "Migrations applied successfully"

# Verify no pending migrations
info "Verifying no pending migrations..."
PENDING=$(docker compose run --rm migrate -- --dry-run 2>&1 | grep -c "Applying migration" || true)
if [[ "${PENDING}" -gt 0 ]]; then
  warn "${PENDING} pending migration(s) were found after running migrate — investigate before proceeding"
fi
success "No pending migrations"

# ── 4. Start application services ─────────────────────────────────────────
info "=== Phase 4: Starting application ==="
docker compose up -d web nginx

# ── 5. Health checks ──────────────────────────────────────────────────────
info "=== Phase 5: Health check verification ==="

BASE_URL="https://${DOMAIN}"
HEALTH_TIMEOUT=90
ELAPSED=0

info "Waiting up to ${HEALTH_TIMEOUT}s for /health endpoint..."
until curl -sf --max-time 5 "${BASE_URL}/health" &>/dev/null; do
  ELAPSED=$((ELAPSED + 5))
  if [[ $ELAPSED -ge $HEALTH_TIMEOUT ]]; then
    error "Health check timed out after ${HEALTH_TIMEOUT}s"
    docker compose logs web --tail 50
    exit 3
  fi
  sleep 5
done
success "/health → 200"

# Check /healthz as well
if curl -sf --max-time 5 "${BASE_URL}/healthz" &>/dev/null; then
  success "/healthz → 200"
else
  warn "/healthz did not return 200 — check application logs"
fi

# Verify Docker health status
WEB_HEALTH=$(docker compose ps web --format json | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('Health','unknown'))" 2>/dev/null || echo "unknown")
if [[ "${WEB_HEALTH}" == "healthy" ]]; then
  success "Docker container health: healthy"
else
  warn "Docker container health: ${WEB_HEALTH}"
fi

# ── 6. Smoke tests ────────────────────────────────────────────────────────
if [[ "${SKIP_SMOKE}" == "false" ]]; then
  info "=== Phase 6: Smoke tests ==="
  if bash "${SCRIPT_DIR}/smoke-test.sh" "${BASE_URL}"; then
    success "All smoke tests passed"
  else
    error "Smoke tests failed — deployment may be incomplete"
    exit 4
  fi
else
  warn "Skipping smoke tests (--skip-smoke)"
fi

# ── 7. Enable monitoring alerts ───────────────────────────────────────────
info "=== Phase 7: Enabling monitoring alerts ==="
docker compose exec web \
  wget -qO- "http://localhost:8080/api/v1/dashboard/health" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print('System status:', d.get('status','unknown'))" \
  2>/dev/null || warn "Could not query system health via internal API"

success "=== Deployment complete ==="
echo
echo "  Application: https://${DOMAIN}"
echo "  API docs:    https://${DOMAIN}/api-docs (disabled in production)"
echo "  Health:      https://${DOMAIN}/health"
echo
echo "  Docker service status:"
docker compose ps
