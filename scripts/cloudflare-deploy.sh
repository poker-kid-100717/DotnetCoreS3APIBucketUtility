#!/usr/bin/env bash
# Renders the Wrangler config and deploys the Worker + ASP.NET Core Container to Cloudflare, with
# objects stored in Cloudflare R2. Used by GitHub Actions; also runnable locally.
#
# Required (unless VALIDATE_ONLY=true): CLOUDFLARE_API_TOKEN, CLOUDFLARE_ACCOUNT_ID,
#   R2_ACCESS_KEY_ID / R2_SECRET_ACCESS_KEY   an R2 API token (Object Read & Write), ideally scoped to the buckets the API may use.
# Optional:
#   DATABASE_URL          MongoDB URL for the object catalog, for example MongoDB Atlas:
#                         mongodb+srv://<user>:<password>@<cluster>.mongodb.net/s3utility. When empty the
#                         catalog is kept in memory and lost when the container restarts.
#   API_KEY               any random string; writes (bucket create/delete, uploads, deletes) need it as X-Api-Key.
#                         Without it the deployed API is read-only.
#   APP_HOST              custom hostname (for example files.example.com). When empty the
#                         Worker is served from its workers.dev URL only.
#   VALIDATE_ONLY=true    run `wrangler deploy --dry-run` without contacting Cloudflare.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CF="$ROOT/cloudflare"
VALIDATE_ONLY="${VALIDATE_ONLY:-false}"
APP_HOST="${APP_HOST:-}"

if [ "$VALIDATE_ONLY" != "true" ]; then
  : "${CLOUDFLARE_API_TOKEN:?CLOUDFLARE_API_TOKEN is required}"
  : "${CLOUDFLARE_ACCOUNT_ID:?CLOUDFLARE_ACCOUNT_ID is required}"
  : "${R2_ACCESS_KEY_ID:?R2_ACCESS_KEY_ID is required}"
  : "${R2_SECRET_ACCESS_KEY:?R2_SECRET_ACCESS_KEY is required}"
else
  CLOUDFLARE_ACCOUNT_ID="${CLOUDFLARE_ACCOUNT_ID:-dry-run-account}"
fi
export APP_HOST CLOUDFLARE_ACCOUNT_ID

echo "==> Preparing Cloudflare Worker"
npm install --no-audit --no-fund --prefix "$CF"

node - "$CF/wrangler.template.jsonc" "$CF/wrangler.generated.jsonc" <<'NODE'
const fs = require("fs");
const [template, output] = process.argv.slice(2);
const config = JSON.parse(fs.readFileSync(template, "utf8"));
config.vars.R2_ENDPOINT = `https://${process.env.CLOUDFLARE_ACCOUNT_ID}.r2.cloudflarestorage.com`;
if (process.env.APP_HOST) {
  config.routes = [{ pattern: process.env.APP_HOST, custom_domain: true }];
}
fs.writeFileSync(output, JSON.stringify(config, null, 2) + "\n");
NODE

if [ -z "${DATABASE_URL:-}" ]; then
  echo "::notice::DATABASE_URL is not set; the object catalog will be kept in memory."
fi
if [ -z "${API_KEY:-}" ]; then
  echo "::notice::API_KEY is not set; the deployed API will refuse writes."
fi
SECRETS_FILE="$(mktemp)"
trap 'rm -f "$SECRETS_FILE"' EXIT
chmod 600 "$SECRETS_FILE"
node > "$SECRETS_FILE" <<'NODE'
const secrets = {};
for (const name of ["R2_ACCESS_KEY_ID", "R2_SECRET_ACCESS_KEY", "DATABASE_URL", "API_KEY"]) {
  if (process.env[name]) secrets[name] = process.env[name];
}
process.stdout.write(JSON.stringify(secrets));
NODE
SECRETS_ARGS=()
if [ "$(cat "$SECRETS_FILE")" != "{}" ]; then SECRETS_ARGS=(--secrets-file "$SECRETS_FILE"); fi

cd "$CF"
if [ "$VALIDATE_ONLY" = "true" ]; then
  npx wrangler deploy --dry-run --outdir "${RUNNER_TEMP:-/tmp}/wrangler-dry-run" \
    --config wrangler.generated.jsonc "${SECRETS_ARGS[@]}"
  echo "Cloudflare dry-run passed."
  exit 0
fi

DEPLOY_LOG="$(mktemp)"
npx wrangler deploy --config wrangler.generated.jsonc "${SECRETS_ARGS[@]}" | tee "$DEPLOY_LOG"

if [ -n "$APP_HOST" ]; then
  APP_URL="https://$APP_HOST"
else
  APP_URL="$(grep -oE 'https://[a-z0-9.-]+\.workers\.dev' "$DEPLOY_LOG" | head -1)"
fi
: "${APP_URL:?could not determine the deployed URL}"

wait_for() {
  url="$1"
  pattern="$2"
  for attempt in $(seq 1 18); do
    body="$(curl --fail --silent --show-error --retry 2 --retry-delay 2 --retry-all-errors "$url" 2>/dev/null || true)"
    if printf '%s' "$body" | grep -q "$pattern"; then
      return 0
    fi
    echo "Waiting for $url (attempt $attempt/18)..."
    sleep 10
  done
  echo "::error::Smoke test failed: $url"
  return 1
}

echo "==> Smoke testing $APP_URL"
wait_for "$APP_URL/health" "Healthy"
wait_for "$APP_URL/" "DotnetCoreS3Utility"
wait_for "$APP_URL/api/catalog" "\["

# Writes without the key must be refused.
code="$(curl --silent --output /dev/null --write-out '%{http_code}' -X POST "$APP_URL/api/bucket/create/smoke-test-unauthorized")"
if [ "$code" != "401" ]; then
  echo "::error::Expected writes without X-Api-Key to be rejected with 401, got HTTP $code."
  exit 1
fi

echo "Deployed: $APP_URL"
if [ -n "${GITHUB_OUTPUT:-}" ]; then echo "url=$APP_URL" >> "$GITHUB_OUTPUT"; fi
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then echo "DotnetCoreS3Utility deployed: $APP_URL" >> "$GITHUB_STEP_SUMMARY"; fi
