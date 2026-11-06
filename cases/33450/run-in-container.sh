#!/usr/bin/env bash
set -euo pipefail

source_dir=/inputs
case_dir=/case
output_dir=/results
revision=8ba9860b555e35f6bdfaf80eeaa087e65fdcca11
build=/tmp/openssl-current

{
    echo "UTC run time: $(date -u +%FT%TZ)"
    echo "Container: $(. /etc/os-release; echo "$PRETTY_NAME")"
    echo "Clang: $(clang --version | head -1)"
    dpkg-query -W -f='${binary:Package}=${Version}\n' clang libclang-rt-18-dev gcc g++ make perl
    echo "Reproducer SHA-256: $(sha256sum /reproducer/ec-group-cmp-nullgen.c | cut -d' ' -f1)"
    echo "Patch SHA-256: $(sha256sum "$case_dir/ec_group_cmp.patch" | cut -d' ' -f1)"
} | tee "$output_dir/environment.txt"

mkdir -p "$build"
tar -xzf "$source_dir/current.tar.gz" -C "$build"
cd "$build"
CC=clang ./Configure enable-asan enable-ubsan no-shared --debug \
    >"$output_dir/configure.log" 2>&1
make -j2 build_generated >>"$output_dir/configure.log" 2>&1
make -j2 libcrypto.a >"$output_dir/build-baseline.log" 2>&1

clang -O1 -g -fsanitize=address,undefined -Iinclude \
    /reproducer/ec-group-cmp-nullgen.c libcrypto.a -ldl -pthread \
    -o /tmp/ec-group-cmp-baseline
set +e
ASAN_OPTIONS=detect_leaks=0 UBSAN_OPTIONS=print_stacktrace=1 \
    /tmp/ec-group-cmp-baseline >"$output_dir/baseline-output.txt" 2>&1
baseline_result=$?
set -e
echo "$baseline_result" >"$output_dir/baseline.exit-code"

patch -p1 <"$case_dir/ec_group_cmp.patch" >"$output_dir/patch-apply.txt"
make -j2 libcrypto.a >"$output_dir/build-patched.log" 2>&1
clang -O1 -g -fsanitize=address,undefined -Iinclude \
    /reproducer/ec-group-cmp-nullgen.c libcrypto.a -ldl -pthread \
    -o /tmp/ec-group-cmp-patched
set +e
ASAN_OPTIONS=detect_leaks=0 UBSAN_OPTIONS=print_stacktrace=1 \
    /tmp/ec-group-cmp-patched >"$output_dir/patched-output.txt" 2>&1
patched_result=$?
set -e
echo "$patched_result" >"$output_dir/patched.exit-code"
