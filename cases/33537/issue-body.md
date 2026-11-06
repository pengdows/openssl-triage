## Summary

`CMS_encrypt()` accepts the 1- and 8-bit CFB ciphers (`AES-{128,192,256}-CFB1` and `-CFB8`) but labels the content with the full-block cipher's identifier (`aes-128-cfb`, 2.16.840.1.101.3.4.1.4). `CMS_decrypt()` of that structure with the right key returns 1 and writes plaintext-length output that is **not** the plaintext, with nothing on the error queue. Every recipient, including this library, decrypts with the wrong cipher.

Only public API calls are involved, with no crafted input. `EVP_CIPHER_get_type()` maps CFB1 and CFB8 to the CFB128 NID, and `cms_enc.c` writes that NID into the message without checking that it still names the cipher used.

The documentation does not exclude these ciphers:

- `CMS_encrypt(3)`: "*cipher* is the symmetric cipher to use." and "The algorithm passed in the *cipher* parameter must support ASN1 encoding of its parameters." CFB1 and CFB8 do (an IV).
- `CMS_decrypt(3)`: `CMS_decrypt()` returns "either 1 for success or 0 for failure."
- `EVP_EncryptInit(3)`: `EVP_CIPHER_get_type()` returns "the NID of the cipher's OBJECT IDENTIFIER or NID_undef if it has no defined OBJECT IDENTIFIER", and "ignores the cipher parameters (40 bit RC2 and 128 bit RC2 have the same NID)". CFB8 is not a parameter variant of CFB128 in that sense; it is a different cipher.
- `EVP_CIPHER-AES(7)` lists "AES-128-CFB1" and "AES-128-CFB8" as separate algorithms.

Measured on master `fd41cfc` (2026-11-03; static ASan+UBSan build, default library context, no property query); same results on `329a985`. The two code sites (the `EVP_CIPHER_get_type()` mapping and the `calg->algorithm` assignment in `cms_enc.c`) are unchanged in `openssl-3.0.22`, `3.5.9`, `3.6.5`, `4.0.3` and `4.1.0`, and the distro 3.6.2 library gives the same results for the six AES rows below, so the release branches are affected too.

## Reproduction

`$H` is the install prefix of an ASan+UBSan static build (`./Configure enable-asan enable-ubsan no-shared`). ASan reports nothing; the defect is in the return values. The program takes cipher names on the command line; with none it prints the table under "Coverage".

```c
/*
 * Build: clang -O1 -fsanitize=address,undefined -I$H/include cms_cfb.c -o cms_cfb $H/lib/libcrypto.a -ldl -pthread
 * Run:   ./cms_cfb AES-128-CFB8     one cipher (any names accepted)
 *        ./cms_cfb                  the table under "Coverage"
 *
 * For each cipher: CMS_encrypt() a 30-byte message to one RSA recipient, then
 * CMS_decrypt() it. Prints the cipher's NID and EVP_CIPHER_get_type(), both
 * return values, the output length, whether the output equals the input, and
 * the first error left on the queue.
 */
#include <stdio.h>
#include <string.h>
#include <openssl/cms.h>
#include <openssl/err.h>
#include <openssl/x509.h>

int main(int argc, char **argv)
{
    static const char *all[] = {
        "AES-128-CFB", "AES-128-CFB1", "AES-128-CFB8",
        "AES-192-CFB", "AES-192-CFB1", "AES-192-CFB8",
        "AES-256-CFB", "AES-256-CFB1", "AES-256-CFB8",
        "DES-EDE3-CFB", "DES-EDE3-CFB1", "DES-EDE3-CFB8",
        "AES-128-OFB", "AES-128-CTR", "AES-128-CBC", "DES-EDE3-CBC", "AES-128-GCM"
    };
    const char **names = argc > 1 ? (const char **)argv + 1 : all;
    int count = argc > 1 ? argc - 1 : (int)(sizeof(all) / sizeof(all[0]));
    const char msg[] = "The quick brown fox jumps over"; /* 30 bytes */
    EVP_PKEY *key = EVP_PKEY_Q_keygen(NULL, NULL, "RSA", (size_t)2048);
    X509 *x = X509_new();
    X509_NAME *nm = X509_NAME_new();
    STACK_OF(X509) *certs = sk_X509_new_null();
    int i;

    X509_set_version(x, X509_VERSION_3);
    ASN1_INTEGER_set(X509_get_serialNumber(x), 7);
    X509_NAME_add_entry_by_txt(nm, "CN", MBSTRING_ASC, (const unsigned char *)"t", -1, -1, 0);
    X509_set_subject_name(x, nm);
    X509_set_issuer_name(x, nm);
    X509_gmtime_adj(X509_getm_notBefore(x), 0);
    X509_gmtime_adj(X509_getm_notAfter(x), 3600);
    X509_set_pubkey(x, key);
    X509_sign(x, key, EVP_sha256());
    sk_X509_push(certs, x);

    for (i = 0; i < count; i++) {
        EVP_CIPHER *c = EVP_CIPHER_fetch(NULL, names[i], NULL);
        BIO *in = BIO_new_mem_buf(msg, sizeof(msg) - 1);
        BIO *out = BIO_new(BIO_s_mem());
        CMS_ContentInfo *cms;
        unsigned long e;
        char *p;
        long n;
        int rc;

        ERR_clear_error();
        cms = CMS_encrypt(certs, in, c, CMS_BINARY);
        rc = cms ? CMS_decrypt(cms, key, x, NULL, out, CMS_BINARY) : -9;
        n = BIO_get_mem_data(out, &p);
        e = ERR_peek_error();
        printf("%-14s nid=%-4d type=%-4d encrypt=%s decrypt=%d out=%ld equal=%s%s%s\n",
               names[i], EVP_CIPHER_get_nid(c), EVP_CIPHER_get_type(c),
               cms ? "ok" : "NULL", rc, n,
               n == (long)sizeof(msg) - 1 && memcmp(p, msg, n) == 0 ? "yes" : "no",
               e ? " err=" : "", e ? ERR_reason_error_string(e) : "");
        CMS_ContentInfo_free(cms);
        BIO_free(in);
        BIO_free(out);
        EVP_CIPHER_free(c);
    }
    sk_X509_free(certs);
    X509_NAME_free(nm);
    X509_free(x);
    EVP_PKEY_free(key);
    return 0;
}
```

Output:

```
$ ./cms_cfb AES-128-CFB8
AES-128-CFB8   nid=653  type=421  encrypt=ok decrypt=1 out=30 equal=no
```

Dumping the structure with the 3.6.2 `openssl cms -cmsout -print` shows `contentEncryptionAlgorithm: algorithm: aes-128-cfb (2.16.840.1.101.3.4.1.4)` and a 16-byte IV; nothing in the message says "8-bit segments". `AES-128-CFB`, `-CBC` and `-OFB` round-trip.

## Coverage

`./cms_cfb` with no arguments runs the same round trip over every AES CFB width and several sibling ciphers.

### Results (master `fd41cfc`)

```
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

- `AES-*-CFB1` and `-CFB8`: `get_type` differs from `get_nid`, `CMS_encrypt()` succeeds, `CMS_decrypt()` returns 1 with 30 bytes that differ from the input.
- `AES-*-CFB`, `-OFB`, `-CBC`, `DES-EDE3-CBC`, `AES-128-GCM`: `get_type` equals `get_nid` and the round trip is exact.
- `AES-128-CTR` has no OID (`get_type` is 0) and `CMS_encrypt()` fails, as expected. CFB1/CFB8 are the case where `EVP_CIPHER_get_type()` returns a non-zero NID that names a different cipher, so that check is passed.
- `DES-EDE3-CFB*` is rejected by `CMS_encrypt()` on master. On the distro 3.6.2 it is accepted, with `get_type` 30 (`DES-CFB`) for CFB1/CFB8, and `CMS_decrypt()` returns 0; that mapping typo was fixed on master after #30966 and is not part of this report.

## Disclaimer

This bug was originally found by our work-in-progress fuzzer. THE ROOT CAUSE ANALYSIS AND THE PROPOSED FIX BELOW THIS POINT ARE PRODUCED BY LLMs, AND ARE FOR INFORMATION ONLY. We only submitted the following bits as it is already used in our pipeline for checking the validity of a fuzzer-found bug, and might be useful in saving you some time in reviewing the issue. Feel free to ignore those following sections otherwise.

## Cause

- `crypto/evp/evp_lib.c:242` `EVP_CIPHER_get_type()`: the switch at lines 260-264 (and the 192/256-bit and 3DES cases after it) returns the `*_cfb128` NID for `*_cfb128`, `*_cfb8` and `*_cfb1`. The function's comment says it converts "the various cipher NIDs and dummies to a proper OID NID"; CFB1 and CFB8 have no OID of their own, so the result is another cipher's OID rather than `NID_undef`.
- `crypto/cms/cms_enc.c:90` `ossl_cms_EncryptedContent_init_bio()` (encrypt branch): `calg->algorithm = OBJ_nid2obj(EVP_CIPHER_CTX_get_type(ctx));`. It falls back to the cipher name only if the NID is `NID_undef` (lines 93-94), so CFB1/CFB8 are accepted with the CFB128 OID. On decrypt (line 64) the receiver resolves that OID with `EVP_get_cipherbyobj()` and gets CFB128.
- CFB is unauthenticated, so decrypting with the wrong segment size does not fail; it yields output of the right length.

## Proposed fix

For the 1- and 8-bit CFB variants `EVP_CIPHER_CTX_get_nid()` differs from `EVP_CIPHER_CTX_get_type()` while the mode is `EVP_CIPH_CFB_MODE`; for CFB128 they are equal. The patch refuses these in `ossl_cms_EncryptedContent_init_bio()` instead of writing a misleading OID. `EVP_CIPHER_get_type()` itself is left alone, since PKCS5 v2 (`p5_pbev2.c`, `p5_scrypt.c`) and the key-wrap code also use it.

```diff
--- a/crypto/cms/cms_enc.c
+++ b/crypto/cms/cms_enc.c
@@ -93,6 +93,17 @@
         if (calg->algorithm == NULL || calg->algorithm->nid == NID_undef)
             calg->algorithm = OBJ_txt2obj(EVP_CIPHER_get0_name(cipher), 0);
 
+        /*
+         * EVP_CIPHER_CTX_get_type() maps the 1- and 8-bit CFB variants to the
+         * identifier of the full-block variant. Recording it would describe a
+         * different cipher than the one used, so refuse these.
+         */
+        if (EVP_CIPHER_CTX_get_mode(ctx) == EVP_CIPH_CFB_MODE
+            && EVP_CIPHER_CTX_get_nid(ctx) != EVP_CIPHER_CTX_get_type(ctx)) {
+            ERR_raise(ERR_LIB_CMS, CMS_R_UNSUPPORTED_CONTENT_ENCRYPTION_ALGORITHM);
+            goto err;
+        }
+
         if (calg->algorithm == NULL || OBJ_length(calg->algorithm) == 0) {
             ERR_raise(ERR_LIB_CMS, CMS_R_UNSUPPORTED_CONTENT_ENCRYPTION_ALGORITHM);
             goto err;
```

With this change in the master build (`fd41cfc`), the six CFB1/CFB8 rows become `encrypt=NULL ... err=unsupported content encryption algorithm` and all other rows are unchanged. The test suite was not run.

## Notes

Severity: medium. Data written with a documented API call and a documented cipher cannot be decrypted by any receiver, and the receiver gets a success return with wrong bytes. The sender is not told either: `CMS_encrypt()` succeeds and the sender's own `CMS_decrypt()` shows the same wrong output.

These ciphers are unusual in CMS (RFC 3565 lists only CBC for AES), so rejecting them in `CMS_encrypt()` is probably the right outcome. `PKCS7_encrypt()` does not call `EVP_CIPHER_get_type()` and was not run. A separate defect with similar symptoms (silent `CMS_decrypt()` success with no output, for AES key-wrap content ciphers) has a different cause and is reported separately.