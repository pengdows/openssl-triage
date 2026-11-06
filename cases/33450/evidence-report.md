# Case #33450: `EC_GROUP_cmp()` with missing generators

## Status

The supplied reproducer crashes current OpenSSL master, and the local candidate patch prevents the crash. This is a local experiment only; no patch or report was sent upstream. The reported short revision `a752594` could not be resolved through the OpenSSL GitHub commit API, so this run uses the pinned current-master commit below.

## Environment

- OpenSSL commit: `8ba9860b555e35f6bdfaf80eeaa087e65fdcca11`
- Container: Ubuntu 24.04.5, image pinned in `Dockerfile`
- Compiler: Clang 18.1.3
- Configuration: `enable-asan enable-ubsan no-shared --debug`
- Reproducer SHA-256: `8d59285d870af8c5fb5441279088d72784052621c3850b3d256628d6a3a5d927`
- Candidate patch SHA-256: recorded in `evidence/reproducer-and-patch.sha256`

## Observed results

| Source | Exit | Result |
|---|---:|---|
| Unmodified pinned source | 1 | UBSan reports member access through a null `EC_POINT` in `crypto/ec/ec_local.h`; the supplied call does not return normally. |
| Candidate patched source | 0 | Prints `EC_GROUP_cmp() returned 0` for the two equal curve groups without generators. |

The patch skips point comparison when both groups lack a generator and handles a one-sided missing generator as a difference. The supplied reproducer confirms the reported failure path and its corrected behavior. OpenSSL's own `ectest` now covers equal generator-less groups, a one-sided missing generator in both argument orders, equal groups with generators, different non-NULL generators, and different curve parameters. The new test fails on the unpatched source and passes after the fix; the full `ectest` executable passes under ASan and UBSan. Upstream review remains outstanding.

## Artifacts and rerun

Run `bash cases/33450/reproduce.sh` from the project root. The script fetches the pinned source archive if absent, then builds and runs the baseline and patched versions in a resource-limited container with networking disabled. Logs, outputs, exit codes, checksums, and image identity are in `evidence/`.

Issue: [openssl/openssl#33450](https://github.com/openssl/openssl/issues/33450).
