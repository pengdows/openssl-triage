#!/usr/bin/env bash
set -euo pipefail

source_dir=/inputs
output_dir=/results
reported=859aea422b5be17ee9fc0f7678e9de302eb67b72
fixed=8daabd4167e53dc3539a9a33a985653813373e86
fuzzer_lib=$(find /usr/lib/llvm-* -path '*/libclang_rt.fuzzer-x86_64.a' -print -quit)
if [[ -z "$fuzzer_lib" ]]; then
    echo "Could not locate libFuzzer runtime" >&2
    exit 2
fi

mkdir -p "$output_dir"
{
    echo "UTC run time: $(date -u +%FT%TZ)"
    echo "Container: $(. /etc/os-release; echo "$PRETTY_NAME")"
    echo "Installed tool packages:"
    dpkg-query -W -f='${binary:Package}=${Version}\n' clang libclang-rt-18-dev gcc g++ make perl
    echo "Clang: $(clang --version | head -1)"
    echo "Fuzzer runtime: $fuzzer_lib"
    echo "Input SHA-256: $(sha256sum "$source_dir/input-16.bin" | cut -d' ' -f1)"
    echo "Input bytes: $(od -An -tx1 -v "$source_dir/input-16.bin" | tr -s ' ' | xargs)"
} | tee "$output_dir/environment.txt"

for revision in "$reported" "$fixed"; do
    name=reported
    [[ "$revision" == "$fixed" ]] && name=fixed
    source="$source_dir/$name.tar.gz"
    build="/tmp/openssl-$name"
    mkdir -p "$build"
    tar -xzf "$source" -C "$build"
    cd "$build"

    echo "=== Configure $name ($revision) ===" | tee "$output_dir/$name.log"
    CC=clang ./config enable-fuzz-libfuzzer \
        --with-fuzzer-lib="$fuzzer_lib" \
        enable-asan enable-ubsan no-shared \
        -DFUZZING_BUILD_MODE_UNSAFE_FOR_PRODUCTION \
        -fsanitize=fuzzer-no-link --debug \
        >>"$output_dir/$name.log" 2>&1
    echo "=== Generate headers ===" | tee -a "$output_dir/$name.log"
    make -j2 build_generated >>"$output_dir/$name.log" 2>&1
    echo "=== Build fuzz/provider ===" | tee -a "$output_dir/$name.log"
    LDCMD=clang++ make -j2 fuzz/provider >>"$output_dir/$name.log" 2>&1

    clang -I. -Iinclude -pthread -m64 -fsanitize=address,undefined \
        -fno-sanitize-recover=all -DFUZZING_BUILD_MODE_UNSAFE_FOR_PRODUCTION \
        -fsanitize=fuzzer-no-link -c fuzz/provider.c -o "/tmp/$name-provider.o"
    clang -pthread -m64 -fsanitize=address,undefined \
        "/tmp/$name-provider.o" /probe.c libcrypto.a -ldl \
        -o "/tmp/$name-return-probe"
    "/tmp/$name-return-probe" | tee "$output_dir/$name-callback.txt"

    set +e
    ASAN_OPTIONS=detect_leaks=0 UBSAN_OPTIONS=print_stacktrace=1 \
        "$build/fuzz/provider" -seed=1 -runs=1 -max_len=16 -verbosity=1 "$source_dir/corpus" \
        >>"$output_dir/$name.log" 2>&1
    result=$?
    set -e
    echo "$result" >"$output_dir/$name.exit-code"
    echo "Run exit code: $result" | tee -a "$output_dir/$name.log"
done

printf 'reported=%s\nfixed=%s\n' \
    "$(cat "$output_dir/reported.exit-code")" \
    "$(cat "$output_dir/fixed.exit-code")" >"$output_dir/exit-codes.txt"
