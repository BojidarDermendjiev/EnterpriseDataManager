# ══════════════════════════════════════════════════════════════════════════════
# vault/policy.hcl — HashiCorp Vault access policy for Enterprise Data Manager
#
# Apply with:
#   vault policy write edm-app vault/policy.hcl
#
# The EDM application service account is granted read-only access to the paths
# below. Write/delete access is intentionally excluded from this policy; secret
# rotation is performed by a separate privileged admin role.
# ══════════════════════════════════════════════════════════════════════════════

# ── Database credentials ──────────────────────────────────────────────────────
path "secret/data/edm/prod/db_password" {
  capabilities = ["read"]
}

# ── JWT signing secret ────────────────────────────────────────────────────────
path "secret/data/edm/prod/jwt_secret" {
  capabilities = ["read"]
}

# ── Webhook HMAC secret ───────────────────────────────────────────────────────
path "secret/data/edm/prod/webhook_secret" {
  capabilities = ["read"]
}

# ── Structured log shipping (Seq) ─────────────────────────────────────────────
path "secret/data/edm/prod/seq_api_key" {
  capabilities = ["read"]
}

# ── Smoke test credentials (CI/CD only) ───────────────────────────────────────
path "secret/data/edm/prod/smoke_test_password" {
  capabilities = ["read"]
}

# ── Allow the app to renew its own token ──────────────────────────────────────
path "auth/token/renew-self" {
  capabilities = ["update"]
}

# ── Allow the app to look up its own token metadata ──────────────────────────
path "auth/token/lookup-self" {
  capabilities = ["read"]
}
