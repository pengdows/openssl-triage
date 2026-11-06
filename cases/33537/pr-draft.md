# PR draft

## Title

Reject AES-CFB1 and CFB8 in CMS encryption (#33537)

## Body

Fixes #33537.

`EVP_CIPHER_CTX_get_type()` maps AES-CFB1 and AES-CFB8 to the AES-CFB128 object identifier. `CMS_encrypt()` used that identifier in the output, so recipients selected CFB128 and `CMS_decrypt()` could return success with incorrect plaintext.

Reject CFB ciphers when the cipher NID differs from the type/OID NID. This prevents CMS from emitting an identifier that describes a different cipher. Standard AES-CFB128 remains supported.

The regression test covers all six AES key-size/segment-size combinations (CFB1 and CFB8 for 128, 192 and 256-bit keys), checks the unsupported-cipher error, and includes an AES-CFB128 round-trip control.

### Verification

- OpenSSL master commit: `8ba9860b555e35f6bdfaf80eeaa087e65fdcca11`.
- TDD: the new test failed on unmodified master because `CMS_encrypt()` returned a CMS object for an ambiguous cipher; it passed after the guard was added.
- Configured Clang with ASan and UBSan (`enable-asan enable-ubsan no-shared --debug`).
- `test/cmsapitest`: all 30 top-level tests passed, including the new regression and CFB128 round-trip.
- OpenSSL recipe `80-test_cmsapi.t`: passed.
- OpenSSL recipe `80-test_cms.t`: passed all 43 tests after building `providers/legacy.so`.
- Full OpenSSL suite from a clean GCC 15.2 `--coverage no-shared` build (`make -j4 test`): **407 recipe files and 3,709 tests passed**. Tests requiring unavailable build modes or external projects were skipped by their documented conditions.
- `gcov` confirms full decision coverage for the new guard: CFB mode true/false counts were 14/62; within CFB mode, cipher NID mismatch/match counts were 12/2. The new regression test function reached 90% block coverage; the unhit blocks are failure exits for setup/assertions.
- The issue's public-API reproducer was also confirmed with the separate OpenSSL Triage harness against the pinned current-master source.

This patch targets OpenSSL master at the commit above. The issue report identifies the same behavior on released branches; backports are outside this change. The issue's reproducer and the independent Triage harness are not included in this PR.
