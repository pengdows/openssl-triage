#!/usr/bin/env bash
set -euo pipefail

case_dir=$(cd "$(dirname "$0")" && pwd)
project_root=$(cd "$case_dir/../.." && pwd)
work_dir="$case_dir/work"
results="$case_dir/evidence"
reported=859aea422b5be17ee9fc0f7678e9de302eb67b72
fixed=8daabd4167e53dc3539a9a33a985653813373e86

command -v docker >/dev/null || { echo "Docker is required" >&2; exit 2; }
mkdir -p "$work_dir" "$results"

if [[ ! -f "$work_dir/reported.tar.gz" || ! -f "$work_dir/fixed.tar.gz" ]]; then
    source_repo=$(mktemp -d)
    trap 'rm -rf "$source_repo"' EXIT
    git -C "$source_repo" init -q
    git -C "$source_repo" remote add origin https://github.com/openssl/openssl.git
    git -C "$source_repo" fetch --depth=1 origin "$reported" "$fixed"
    git -C "$source_repo" archive --format=tar.gz -o "$work_dir/reported.tar.gz" "$reported"
    git -C "$source_repo" archive --format=tar.gz -o "$work_dir/fixed.tar.gz" "$fixed"
fi

printf '\000%.0s' {1..16} >"$work_dir/input-16.bin"
mkdir -p "$work_dir/corpus"
cp "$work_dir/input-16.bin" "$work_dir/corpus/16-zero-bytes"

docker build --tag openssl-triage-32897:local "$case_dir"
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
    openssl-triage-32897:local

cp "$work_dir/input-16.bin" "$results/input-16.bin"
sha256sum "$results/input-16.bin" >"$results/input-16.bin.sha256"
sha256sum "$work_dir/reported.tar.gz" "$work_dir/fixed.tar.gz" >"$results/source-archives.sha256"
docker image inspect openssl-triage-32897:local --format 'Container image ID: {{.Id}}' >"$results/container-image.txt"
cat "$results/exit-codes.txt"
