# Case 32897: fuzz/provider.c: FuzzerTestOneInput return value

**Triage status:** resolved-upstream  
**Evidence status:** Reproduced across pinned revisions  
**Issue:** https://github.com/openssl/openssl/issues/32897

## Claim

The provider fuzz target returned 1 for inputs of 16 bytes or more, although the libFuzzer callback contract reserves nonzero values for rejection or future use.

## Contract reference

https://github.com/llvm/llvm-project/blob/5a50c15fa686c9c49b78631ac42b133340867ae3/llvm/docs/LibFuzzer.md

## Revision anchors

- Reported source: [`859aea422b5be17ee9fc0f7678e9de302eb67b72`](https://github.com/openssl/openssl/tree/859aea422b5be17ee9fc0f7678e9de302eb67b72)
- Upstream fix: [`8daabd4167e53dc3539a9a33a985653813373e86`](https://github.com/openssl/openssl/commit/8daabd4167e53dc3539a9a33a985653813373e86)

## Evidence collected

Direct calls to the actual callback returned `1` on the reported revision and `0` on the fixed revision for identical 16-byte input.

- Reported revision callback: `1`
- Fixed revision callback: `0`
- 16-byte input SHA-256: `374708fff7719dd5979ec875d56cd2286f6d3cf7ec317a3b25632aab28ec37bb`
- libFuzzer run summaries: reported: #2	INITED exec/s: 0 rss: 70Mb; #2	DONE   corp: 1/1b lim: 4 exec/s: 0 rss: 70Mb; fixed: #2	INITED cov: 95 ft: 96 corp: 1/16b exec/s: 0 rss: 70Mb; #2	DONE   cov: 95 ft: 96 corp: 1/16b lim: 16 exec/s: 0 rss: 70Mb
- Container/toolchain:

```text
UTC run time: 2026-11-05T18:00:09Z
Container: Ubuntu 24.04.5 LTS
Clang: Ubuntu clang version 18.1.3 (1ubuntu1)
Fuzzer runtime: /usr/lib/llvm-18/lib/clang/18/lib/linux/libclang_rt.fuzzer-x86_64.a
Input SHA-256: 374708fff7719dd5979ec875d56cd2286f6d3cf7ec317a3b25632aab28ec37bb
Input bytes: 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
Container base: ubuntu:24.04@sha256:fe81560990fae86dc4743202b2cb4d3df98d0bcf949173cecef15ce1127ef852
clang=1:18.0-59~exp2
g++=4:13.2.0-7ubuntu1
gcc=4:13.2.0-7ubuntu1
libclang-rt-18-dev:amd64=1:18.1.3-1ubuntu1
make=4.3-4.1build2
perl=5.38.2-3.2ubuntu0.6
Container image ID: sha256:b8433605eeda3cfb3f2f98fdbe331391c62bf29463a4255e33526fb87c45f3d2
```

The upstream fix commit also states that nonzero returns discard input and changes this callback to always return `0`. The direct probe and libFuzzer behavior above are observations collected by this project.

## Reproduction

Re-run with `bash cases/32897/reproduce.sh`. The script fetches the pinned source commits, creates the 16-byte input, builds in Docker, then executes with container networking disabled and CPU, memory, process, and capability limits.

## Assessment

Upstream merged a change addressing the reported behavior. The return-value regression is reproduced on the cited revision and absent on the fixed revision. This case is a historical workflow fixture, not a fresh unresolved report.
