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
