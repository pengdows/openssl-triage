# PR draft

## Title

Avoid NULL generator dereference in EC_GROUP_cmp (#33450)

## Body

Fixes #33450.

`EC_GROUP_cmp()` can pass a NULL generator to `EC_POINT_cmp()`, which dereferences the point. This occurs when comparing two groups created from the same curve parameters without setting a generator.

Compare generators only when both groups have one. If exactly one group has a generator, report the groups as different. If neither has one, continue comparing the remaining group parameters.

Add `ectest` coverage for equal groups without generators, one-sided missing
generators in both argument orders, equal groups with generators, and different
non-NULL generators. This covers both outcomes of the generator checks and the
point-comparison success and mismatch paths.

### Testing

- Configured with `CC=clang ./Configure enable-asan enable-ubsan no-shared --debug`.
- `make -j2 test/ectest`.
- `ASAN_OPTIONS=detect_leaks=0 UBSAN_OPTIONS=print_stacktrace=1 ./test/ectest` — passed all 21 top-level tests, including `group_cmp_missing_generator_test`.
- The added test fails on the unpatched source with a UBSan null-pointer diagnostic at the original `EC_POINT_cmp()` call.
- Cross-check: the separate OpenSSL Triage harness runs the issue's supplied reproducer against the pinned source and the candidate patch; UBSan reports the null dereference before the patch, and the patched call returns `0`.

The reproducer was run against OpenSSL commit `8ba9860b555e35f6bdfaf80eeaa087e65fdcca11`. The issue's cited short revision `a752594` could not be resolved, so this is not a reproduction at that reported revision.
