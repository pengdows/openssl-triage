# Case 33537: CMS_encrypt mislabels AES CFB1/CFB8

**Issue status:** open-as-checked-2026-11-05  
**Evidence status:** Reproduced on both pinned revisions  
**Issue:** https://github.com/openssl/openssl/issues/33537

## Claim

CMS_encrypt accepts AES-CFB1 and AES-CFB8, records the AES-CFB128 OID, then CMS_decrypt reports success but returns bytes that differ from the plaintext.

## Revisions

- reported: [`fd41cfc4a1403b74c0541f19cf0d9762aa29124c`](https://github.com/openssl/openssl/tree/fd41cfc4a1403b74c0541f19cf0d9762aa29124c)
- current: [`8ba9860b555e35f6bdfaf80eeaa087e65fdcca11`](https://github.com/openssl/openssl/tree/8ba9860b555e35f6bdfaf80eeaa087e65fdcca11)

## Observed output

The public C reproducer supplied in the issue was run unchanged against both revisions. It reproduced the same mismatch in AES-CFB1 and AES-CFB8; the listed CFB128, CBC, OFB, and GCM controls round-tripped exactly.

Reported revision:

```text
AES-128-CFB    nid=421  type=421  encrypt=ok decrypt=1 out=30 equal=yes
AES-128-CFB1   nid=650  type=421  encrypt=ok decrypt=1 out=30 equal=no
AES-128-CFB8   nid=653  type=421  encrypt=ok decrypt=1 out=30 equal=no
AES-192-CFB    nid=425  type=425  encrypt=ok decrypt=1 out=30 equal=yes
AES-192-CFB1   nid=651  type=425  encrypt=ok decrypt=1 out=30 equal=no
AES-192-CFB8   nid=654  type=425  encrypt=ok decrypt=1 out=30 equal=no
AES-256-CFB    nid=429  type=429  encrypt=ok decrypt=1 out=30 equal=yes
AES-256-CFB1   nid=652  type=429  encrypt=ok decrypt=1 out=30 equal=no
AES-256-CFB8   nid=655  type=429  encrypt=ok decrypt=1 out=30 equal=no
DES-EDE3-CFB   nid=61   type=61   encrypt=NULL decrypt=-9 out=0 equal=no err=unsupported content encryption algorithm
DES-EDE3-CFB1  nid=658  type=61   encrypt=NULL decrypt=-9 out=0 equal=no err=unsupported content encryption algorithm
DES-EDE3-CFB8  nid=659  type=61   encrypt=NULL decrypt=-9 out=0 equal=no err=unsupported content encryption algorithm
AES-128-OFB    nid=420  type=420  encrypt=ok decrypt=1 out=30 equal=yes
AES-128-CTR    nid=904  type=0    encrypt=NULL decrypt=-9 out=0 equal=no err=unsupported content encryption algorithm
AES-128-CBC    nid=419  type=419  encrypt=ok decrypt=1 out=30 equal=yes
DES-EDE3-CBC   nid=44   type=44   encrypt=ok decrypt=1 out=30 equal=yes
AES-128-GCM    nid=895  type=895  encrypt=ok decrypt=1 out=30 equal=yes
```

Current revision:

```text
AES-128-CFB    nid=421  type=421  encrypt=ok decrypt=1 out=30 equal=yes
AES-128-CFB1   nid=650  type=421  encrypt=ok decrypt=1 out=30 equal=no
AES-128-CFB8   nid=653  type=421  encrypt=ok decrypt=1 out=30 equal=no
AES-192-CFB    nid=425  type=425  encrypt=ok decrypt=1 out=30 equal=yes
AES-192-CFB1   nid=651  type=425  encrypt=ok decrypt=1 out=30 equal=no
AES-192-CFB8   nid=654  type=425  encrypt=ok decrypt=1 out=30 equal=no
AES-256-CFB    nid=429  type=429  encrypt=ok decrypt=1 out=30 equal=yes
AES-256-CFB1   nid=652  type=429  encrypt=ok decrypt=1 out=30 equal=no
AES-256-CFB8   nid=655  type=429  encrypt=ok decrypt=1 out=30 equal=no
DES-EDE3-CFB   nid=61   type=61   encrypt=NULL decrypt=-9 out=0 equal=no err=unsupported content encryption algorithm
DES-EDE3-CFB1  nid=658  type=61   encrypt=NULL decrypt=-9 out=0 equal=no err=unsupported content encryption algorithm
DES-EDE3-CFB8  nid=659  type=61   encrypt=NULL decrypt=-9 out=0 equal=no err=unsupported content encryption algorithm
AES-128-OFB    nid=420  type=420  encrypt=ok decrypt=1 out=30 equal=yes
AES-128-CTR    nid=904  type=0    encrypt=NULL decrypt=-9 out=0 equal=no err=unsupported content encryption algorithm
AES-128-CBC    nid=419  type=419  encrypt=ok decrypt=1 out=30 equal=yes
DES-EDE3-CBC   nid=44   type=44   encrypt=ok decrypt=1 out=30 equal=yes
AES-128-GCM    nid=895  type=895  encrypt=ok decrypt=1 out=30 equal=yes
```

## Toolchain and checksums

```text
UTC run time: 2026-11-05T18:32:12Z
Container: Ubuntu 24.04.5 LTS
Clang: Ubuntu clang version 18.1.3 (1ubuntu1)
Installed tool packages:
clang=1:18.0-59~exp2
g++=4:13.2.0-7ubuntu1
gcc=4:13.2.0-7ubuntu1
libclang-rt-18-dev:amd64=1:18.1.3-1ubuntu1
make=4.3-4.1build2
perl=5.38.2-3.2ubuntu0.6
Reproducer SHA-256: 90048719de806ab573b2e7e8eb7134bc38f5eef09c00bc18622f22b938db4f2c
91f74a05433e8f8c54bf06a4b3281e3a498d0a98e6ce63f85f7aee8e8c3f715d  /home/alaricd/prj/openssl-triage/cases/33537/work/reported.tar.gz
ed9fac0c8fc54c2215c247eab8e3cceb514674d2da45a178748774992bec39c8  /home/alaricd/prj/openssl-triage/cases/33537/work/current.tar.gz
90048719de806ab573b2e7e8eb7134bc38f5eef09c00bc18622f22b938db4f2c  /home/alaricd/prj/openssl-triage/cases/33537/cms_cfb.c
Container image ID: sha256:9a1193ff924e290d3392d4ceadd7b882a447812179390dae51a41156646387f9
```

## Reproduction

Re-run with `bash cases/33537/reproduce.sh`. The source builds and executes in a network-disabled Docker container with CPU, memory, process, and capability limits.

## Candidate fix and OpenSSL-suite regression

The fix is being prepared on branch `codex/cms-cfb-correctness` from current OpenSSL master `8ba9860b555e35f6bdfaf80eeaa087e65fdcca11`. CMS encryption now rejects CFB-mode ciphers when the cipher NID differs from the type/OID NID; the ambiguity is what caused AES-CFB1/CFB8 to be emitted as CFB128. The test covers AES-128/192/256-CFB1 and CFB8 and verifies AES-CFB128 still round-trips.

TDD evidence: with the new regression test registered and no code change, the `cmsapitest` executable failed because `CMS_encrypt()` returned a non-NULL CMS object for the first ambiguous cipher. After the guard was added, the test passed.

Focused runs used a Clang ASan+UBSan build:

- `test/cmsapitest`: all 30 top-level tests passed (initial standalone invocation skipped legacy-provider-only subtests until the legacy provider was built).
- `test/recipes/80-test_cmsapi.t`: passed; see `evidence-cmsapi.log`.
- `test/recipes/80-test_cms.t`: passed all 43 tests after building `providers/legacy.so`; see `evidence-cms.log`.
- `git diff --check`: passed.

The full OpenSSL suite was then run from a clean GCC 15.2 `--coverage no-shared` build with `make -j4 test`: **407 test recipe files and 3,709 tests passed**. OpenSSL reported capability/configuration skips for tests requiring unsupported build modes or external projects. The run log is `/tmp/openssl-full-test.log` in the worktree.

GCC `gcov` on the full-suite run confirms both outcomes of each changed predicate: `EVP_CIPHER_CTX_get_mode(ctx) == EVP_CIPH_CFB_MODE` was true 14 times and false 62 times; when true, the NID mismatch was true 12 times and false twice. Thus all outcomes in the new decision guard executed. `test_encrypt_rejects_ambiguous_cfb_oid` reports 90% block coverage; the unhit blocks are setup/assertion failure exits, while its six cipher cases and loop exit all ran. Detailed reports are in `coverage/` and `evidence-gcov-cms-enc.log` / `evidence-gcov-cmsapitest.log`.
