#!/usr/bin/env bash
set -euo pipefail

source_dir=/inputs
output_dir=/results
reported=fd41cfc4a1403b74c0541f19cf0d9762aa29124c
current=8ba9860b555e35f6bdfaf80eeaa087e65fdcca11

{
    echo "UTC run time: $(date -u +%FT%TZ)"
    echo "Container: $(. /etc/os-release; echo "$PRETTY_NAME")"
    echo "Clang: $(clang --version | head -1)"
    echo "Installed tool packages:"
    dpkg-query -W -f='${binary:Package}=${Version}\n' clang libclang-rt-18-dev gcc g++ make perl
    echo "Reproducer SHA-256: $(sha256sum /reproducer/cms_cfb.c | cut -d' ' -f1)"
} | tee "$output_dir/environment.txt"

for entry in "reported:$reported" "current:$current"; do
    name=${entry%%:*}
    revision=${entry#*:}
    build="/tmp/openssl-$name"
    mkdir -p "$build"
    tar -xzf "$source_dir/$name.tar.gz" -C "$build"
    cd "$build"

    echo "=== Configure $name ($revision) ===" | tee "$output_dir/$name.log"
    CC=clang ./Configure enable-asan enable-ubsan no-shared \
        --prefix="/opt/openssl-$name" >>"$output_dir/$name.log" 2>&1
    echo "=== Generate headers and build static libraries ===" | tee -a "$output_dir/$name.log"
    make -j2 build_generated >>"$output_dir/$name.log" 2>&1
    make -j2 build_libs >>"$output_dir/$name.log" 2>&1
    echo "=== Compile reproducer ===" | tee -a "$output_dir/$name.log"
    clang -O1 -fsanitize=address,undefined -Iinclude /reproducer/cms_cfb.c \
        libcrypto.a -ldl -pthread -o "/tmp/cms-cfb-$name" >>"$output_dir/$name.log" 2>&1

    set +e
    ASAN_OPTIONS=detect_leaks=0 UBSAN_OPTIONS=print_stacktrace=1 \
        "/tmp/cms-cfb-$name" >"$output_dir/$name-output.txt" 2>&1
    result=$?
    set -e
    echo "$result" >"$output_dir/$name.exit-code"
    cat "$output_dir/$name-output.txt" | tee -a "$output_dir/$name.log"
    echo "Run exit code: $result" | tee -a "$output_dir/$name.log"
done

printf 'reported=%s\ncurrent=%s\n' \
    "$(cat "$output_dir/reported.exit-code")" \
    "$(cat "$output_dir/current.exit-code")" >"$output_dir/exit-codes.txt"
