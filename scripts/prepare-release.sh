#!/usr/bin/env bash
set -euo pipefail

version="${1:?Usage: scripts/prepare-release.sh <version>}"

python3 - "$version" <<'PY'
import json
import sys
from pathlib import Path

version = sys.argv[1]
path = Path("version.json")
data = json.loads(path.read_text())
data["version"] = version
path.write_text(json.dumps(data, indent=2) + "\n")
PY

python -m towncrier build --version "$version" --yes

git add version.json CHANGELOG.md changelog.d
git commit -m "Prepare v$version"
git tag -a "v$version" -m "ratbot v$version"
