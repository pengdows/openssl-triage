<!--
Thank you for your bug report. If this is your first one,
please take the time to read the following lines before posting it.

NOTE:

    If you're asking about how to use OpenSSL, this isn't the right
    forum.  Please see our User Support resources:
    https://github.com/openssl/openssl/blob/master/SUPPORT.md

Please remember to tell us in what OpenSSL version you found the issue.

For build issues:

    If this is a build issue, please include the configuration output
    as well as a log of all errors.  Don't forget to include the exact
    commands you typed.

    With OpenSSL before 1.1.1, the configuration output comes from the
    configuration command.  With OpenSSL 1.1.1 and on, it's the output
    of `perl configdata.pm --dump`

For other issues:

    If it isn't a build issue, example code or commands to reproduce
    the issue is highly appreciated.
    Also, please remember to tell us if you worked with your own
    OpenSSL build or if it is system provided.

Please remember to put ``` lines before and after any commands plus
output and code, like this:

    ```
    $ echo output output output
    output output output
    ```

    ```
    #include <stdio.h>

    int main() {
        int foo = 1;
        printf("%d\n", foo);
    }
    ```
-->


### Summary

`EC_GROUP_cmp()` passes `EC_GROUP_get0_generator(a)` and `EC_GROUP_get0_generator(b)` to `EC_POINT_cmp()` without checking them. A group made with `EC_GROUP_new_curve_GFp()` (or `EC_GROUP_new()` + `EC_GROUP_set_curve()`) has no generator until `EC_GROUP_set_generator()` is called. Comparing two such groups with identical curve parameters therefore reads `point->meth` from a NULL point in `ec_point_is_compat()`. 


Affected version: a752594


### Reproduction

```
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
```

### Behavior

```
calling EC_GROUP_cmp()
AddressSanitizer:DEADLYSIGNAL
=================================================================
==2264820==ERROR: AddressSanitizer: SEGV on unknown address 0x000000000000 (pc 0x7f9ed9c6ea1e bp 0x7fffaec1dfc0 sp 0x7fffaec1df40 T0)
==2264820==The signal is caused by a READ memory access.
==2264820==Hint: address points to the zero page.
    #0 0x7f9ed9c6ea1e in ec_point_is_compat ../openssl-src/crypto/ec/ec_local.h:335
    #1 0x7f9ed9c6e3cd in EC_POINT_cmp .../build/../openssl-src/crypto/ec/ec_lib.c:1070
    #2 0x7f9ed9c6e0df in EC_GROUP_cmp .../build/../openssl-src/crypto/ec/ec_lib.c:704
    #3 0x55588816465b in main .../b8.c:41
    #4 0x7f9ed9029d8f in __libc_start_call_main ../sysdeps/nptl/libc_start_call_main.h:58
    #5 0x7f9ed9029e3f in __libc_start_main_impl ../csu/libc-start.c:392
    #6 0x5558881642c4 in _start (.../b8+0x12c4)
```

### Suggested fix

`EC_GROUP_cmp()` should handle groups with a NULL generator before passing the generator pointers to `EC_POINT_cmp()`. Currently, comparing two groups created through supported APIs can result in a NULL pointer dereference instead of returning a comparison result or an error.


### Disclaimer

We used Claude Opus to develop the fuzzer that originally discovered this issue, and ChatGPT assisted with preparing this report.


