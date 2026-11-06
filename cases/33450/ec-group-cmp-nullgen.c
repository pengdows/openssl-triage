#include <stdio.h>
#include <openssl/opensslv.h>
#include <openssl/crypto.h>
#include <openssl/bn.h>
#include <openssl/ec.h>
#include <openssl/err.h>

/* p, a, b of prime192v1. Any valid curve works. */
static const char *p_hex = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFFFFFFFFFFFF";
static const char *a_hex = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFFFFFFFFFFFC";
static const char *b_hex = "64210519E59C80E70FA7E9AB72243049FEB8DEECC146B9B1";

int main(void)
{
    BIGNUM *p = NULL, *a = NULL, *b = NULL;
    EC_GROUP *g, *h;
    BN_CTX *ctx = BN_CTX_new();
    int rv;

    if (!BN_hex2bn(&p, p_hex) || !BN_hex2bn(&a, a_hex) || !BN_hex2bn(&b, b_hex))
        return 1;

    g = EC_GROUP_new_curve_GFp(p, a, b, ctx);   /* curve set, no generator */
    h = EC_GROUP_dup(g);                         /* same curve parameters  */
    if (g == NULL || h == NULL) {
        ERR_print_errors_fp(stderr);
        return 1;
    }
    printf("calling EC_GROUP_cmp()\n");
    fflush(stdout);

    rv = EC_GROUP_cmp(g, h, NULL);

    printf("EC_GROUP_cmp() returned %d\n", rv);
    EC_GROUP_free(g);
    EC_GROUP_free(h);
    BN_free(p);
    BN_free(a);
    BN_free(b);
    BN_CTX_free(ctx);
    return 0;
}
