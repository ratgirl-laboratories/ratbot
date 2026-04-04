# GitHub Actions Deploy Notes

## Workflows

- `.github/workflows/build-image.yml`
  - Builds the bot container image from `Dockerfile`
  - Pushes to GHCR
  - Triggered on push to `master` and `development`, plus manual dispatch
  - Publishes tags:
    - immutable: `sha-<full_commit_sha>`
    - branch: `master` / `development`
    - moving aliases: `latest-production`, `latest-staging`

- `.github/workflows/deploy-vps.yml`
  - Auto deploy mapping:
    - `master` build completion -> deploy `production`
    - `development` build completion -> deploy `staging`
  - Manual dispatch supports:
    - `shared`
    - `production`
    - `staging`
  - Uses explicit Compose project names:
    - `ratbot-shared`
    - `ratbot-production`
    - `ratbot-staging`
  - Prevents overlap with per-target concurrency groups

## Required GitHub Secrets and Variables

Recommended names:

- Secret: `VPS_SSH_PRIVATE_KEY`
  - Private key for CI/CD SSH access
  - Key should match `/home/deploy/.ssh/authorized_keys` on VPS
- Secret: `VPS_SSH_KNOWN_HOSTS` (recommended)
  - Output of `ssh-keyscan -H <host>` for strict host key pinning
- Secret: `VPS_HOST`
  - VPS hostname or IP
- Secret: `VPS_PORT` (optional)
  - SSH port, defaults to `22` when empty
- Secret: `VPS_USER` (optional)
  - Defaults to `deploy` when empty

You can store these at repository scope, or per GitHub Environment (`shared`, `staging`, `production`) for stronger isolation/approval controls.

## VPS-side Prerequisites

- Directories must already exist and contain correct compose files + `.env` files:
  - `/opt/ratbot/shared`
  - `/opt/ratbot/production`
  - `/opt/ratbot/staging`
- The workflow does not create or upload `.env` files.
- `deploy` user must:
  - Be allowed to SSH
  - Be able to run Docker/Compose (typically via `docker` group)
- Docker Compose plugin must be installed on the VPS.

## GHCR Pull Authentication Model

The deploy workflow does not send GHCR credentials from GitHub Actions to the VPS.  
Expected model: the VPS `deploy` user is already authenticated for pulls.

One-time setup on VPS (as `deploy`):

```bash
docker login ghcr.io
```

This stores credentials in `~/.docker/config.json` for future `docker compose pull`.

For private images, use a GitHub token/PAT with `read:packages` scope for that login.

## Manual Deploy Usage

Use **Actions -> Deploy RatBot to VPS -> Run workflow**:

- `target=shared`: deploy shared stack only
- `target=production`: deploy production bot stack
- `target=staging`: deploy staging bot stack
- Optional `image_tag` for production/staging:
  - Tag only: `sha-<commit_sha>` or `latest-production` / `latest-staging`
  - Or full image ref: `ghcr.io/<owner>/<repo>:sha-<commit_sha>`

When `image_tag` is omitted:

- production defaults to `latest-production`
- staging defaults to `latest-staging`

The deploy helper writes simple rollback state on the VPS per stack:

- `.deploy-state/current-image.txt`
- `.deploy-state/previous-image.txt`
