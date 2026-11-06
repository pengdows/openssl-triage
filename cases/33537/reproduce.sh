#!/usr/bin/env bash
set -euo pipefail

case_dir=$(cd "$(dirname "$0")" && pwd)
project_root=$(cd "$case_dir/../.." && pwd)
work_dir="$case_dir/work"
results="$case_dir/evidence"
reported=fd41cfc4a1403b74c0541f19cf0d9762aa29124c
current=8ba9860b555e35f6bdfaf80eeaa087e65fdcca11

command -v docker >/dev/null || { echo "Docker is required" >&2; exit 2; }
mkdir -p "$work_dir" "$results"

if [[ ! -f "$work_dir/reported.tar.gz" || ! -f "$work_dir/current.tar.gz" ]]; then
    source_repo=$(mktemp -d)
    trap 'rm -rf "$source_repo"' EXIT
    git -C "$source_repo" init -q
    git -C "$source_repo" remote add origin https://github.com/openssl/openssl.git
    git -C "$source_repo" fetch --depth=1 origin "$reported" "$current"
    git -C "$source_repo" archive --format=tar.gz -o "$work_dir/reported.tar.gz" "$reported"
    git -C "$source_repo" archive --format=tar.gz -o "$work_dir/current.tar.gz" "$current"
fi

docker build --tag openssl-triage-33537:local "$case_dir"
docker run --rm \
    --network none \
    --user "$(id -u):$(id -g)" \
    --read-only \
    --tmpfs "/tmp:rw,exec,nosuid,size=6g,uid=$(id -u),gid=$(id -g)" \
    --cap-drop ALL \
    --security-opt no-new-privileges \
    --pids-limit 256 \
    --memory 6g \
    --cpus 4 \
    --mount "type=bind,src=$work_dir,dst=/inputs,readonly" \
    --mount "type=bind,src=$results,dst=/results" \
    openssl-triage-33537:local

sha256sum "$work_dir/reported.tar.gz" "$work_dir/current.tar.gz" >"$results/source-archives.sha256"
sha256sum "$case_dir/cms_cfb.c" >"$results/reproducer.sha256"
docker image inspect openssl-triage-33537:local --format 'Container image ID: {{.Id}}' >"$results/container-image.txt"
cat "$results/exit-codes.txt"
