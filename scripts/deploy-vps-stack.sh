#!/usr/bin/env bash
set -euo pipefail

stack=""
project=""
remote_dir=""
image_ref=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --stack)
      stack="${2:-}"
      shift 2
      ;;
    --project)
      project="${2:-}"
      shift 2
      ;;
    --remote-dir)
      remote_dir="${2:-}"
      shift 2
      ;;
    --image-ref)
      image_ref="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ -z "$stack" || -z "$project" || -z "$remote_dir" ]]; then
  echo "Missing required arguments." >&2
  usage
  exit 1
fi

if [[ "$stack" != "shared" && "$stack" != "production" && "$stack" != "staging" ]]; then
  echo "Invalid --stack value: $stack" >&2
  exit 1
fi

: "${VPS_HOST:?VPS_HOST is required}"
: "${VPS_SSH_PRIVATE_KEY:?VPS_SSH_PRIVATE_KEY is required}"

vps_port="${VPS_PORT:-22}"
vps_user="${VPS_USER:-deploy}"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

key_file="$tmp_dir/id_key"
known_hosts_file="$tmp_dir/known_hosts"

printf '%s\n' "$VPS_SSH_PRIVATE_KEY" > "$key_file"
chmod 600 "$key_file"

if [[ -n "${VPS_SSH_KNOWN_HOSTS:-}" ]]; then
  printf '%s\n' "$VPS_SSH_KNOWN_HOSTS" > "$known_hosts_file"
else
  ssh-keyscan -H -p "$vps_port" "$VPS_HOST" > "$known_hosts_file" 2>/dev/null
fi
chmod 600 "$known_hosts_file"

ssh_opts=(
  -i "$key_file"
  -o IdentitiesOnly=yes
  -o StrictHostKeyChecking=yes
  -o UserKnownHostsFile="$known_hosts_file"
  -p "$vps_port"
)

image_ref_remote="$image_ref"

ssh "${ssh_opts[@]}" "${vps_user}@${VPS_HOST}" \
  "STACK='$stack' PROJECT='$project' REMOTE_DIR='$remote_dir' IMAGE_REF='$image_ref_remote' bash -s" <<'REMOTE_EOF'
set -euo pipefail

if [[ ! -d "$REMOTE_DIR" ]]; then
  echo "Remote directory does not exist: $REMOTE_DIR" >&2
  exit 1
fi

cd "$REMOTE_DIR"

if ! command -v docker >/dev/null 2>&1; then
  echo "docker command not found on remote host." >&2
  exit 1
fi

if [[ -n "${IMAGE_REF}" ]]; then
  export RATBOT_IMAGE="$IMAGE_REF"
fi

docker compose -p "$PROJECT" pull
docker compose -p "$PROJECT" up -d --remove-orphans

if [[ -n "${IMAGE_REF}" ]]; then
  state_dir=".deploy-state"
  mkdir -p "$state_dir"

  if [[ -f "$state_dir/current-image.txt" ]]; then
    cp "$state_dir/current-image.txt" "$state_dir/previous-image.txt"
  fi

  printf '%s\n' "$IMAGE_REF" > "$state_dir/current-image.txt"
fi

docker compose -p "$PROJECT" ps
REMOTE_EOF
