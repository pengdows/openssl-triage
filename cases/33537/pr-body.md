Fixes #33537.

`EVP_CIPHER_CTX_get_type()` maps AES-CFB1 and AES-CFB8 to the AES-CFB128 object identifier. `CMS_encrypt()` used that identifier in the output, so recipients selected CFB128 and `CMS_decrypt()` could return success with incorrect plaintext.

Reject CFB-mode ciphers when their cipher NID differs from the type/OID NID. This prevents CMS from emitting an identifier that describes a different cipher. Standard AES-CFB128 remains supported.

The regression test covers all six AES key-size/segment-size combinations (CFB1 and CFB8 for 128, 192 and 256-bit keys), checks the unsupported-content-encryption error, and includes an AES-CFB128 round-trip control.

### Verification

- A Clang ASan+UBSan build configured from OpenSSL master commit `8ba9860b555e35f6bdfaf80eeaa087e65fdcca11`.
- TDD: the added test failed on unmodified master because `CMS_encrypt()` returned a CMS object for an ambiguous cipher; it passed after the guard was added.
- `test/cmsapitest`: all 30 top-level tests passed, including the new regression and CFB128 control.
- OpenSSL recipes `80-test_cmsapi.t` and `80-test_cms.t`: passed; the latter passed all 43 tests after building the legacy provider.
- Full OpenSSL test suite, clean GCC 15.2 `--coverage no-shared` build: `make -j4 test` passed all 407 recipe files and 3,709 tests. Capability/configuration skips were reported by tests requiring other build modes or external projects.
- `gcov` confirms all four outcomes in the new decision guard were exercised (CFB mode true/false: 14/62; cipher NID mismatch/match within CFB mode: 12/2). The new regression test function had 90% block coverage; only setup/assertion failure exits were unhit.
- The OpenSSL Triage harness independently reproduced the issue using the public-API reproducer from #33537 against the pinned current-master source.

This patch targets master at the commit above. The issue report notes the same behavior on released branches; this PR does not include backports. The Triage harness is a separate local conformance tool and is not included in this PR.
