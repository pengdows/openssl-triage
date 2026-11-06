# MVP plan

## Goal

Turn one existing public report into a compact, repeatable evidence package that a maintainer can inspect without adopting this tool.

## Milestone 1: historical reproduction for #32897

1. Pin full OpenSSL source revisions for the issue's cited source and the upstream fix.
2. Add a minimal regression harness that invokes or checks the actual fuzz callback behavior, not a hand-copied simulation.
3. Document deterministic build commands and required compiler/libFuzzer versions.
4. Run both revisions in an isolated container with network disabled and resource limits; preserve logs, tool versions, input bytes, and checksums.
5. Generate a report that states what was run and distinguishes upstream's fix rationale from locally observed results.

**Complete.** The project builds the real `fuzz/provider` target and separately calls the real callback using identical 16-byte input at both pinned revisions. The callback returned `1` at the reported revision and `0` at the upstream fix. The libFuzzer log showed no coverage counters on the reported revision and 95 counters on the fixed revision. Logs, input, checksums, toolchain, and a generated report are in `cases/32897/evidence/`.

The issue was fixed upstream, so this validates the workflow against a known historical case. It is not a candidate report to send OpenSSL.

## First open-report validation: #33537

**Reproduced on 2026-11-05.** The issue's supplied public C reproducer returned the same CFB1/CFB8 mismatch on its cited commit and the then-current `master` commit. Control ciphers round-tripped correctly. The issue was still open with no comments or linked PR when checked. This validates running and recording an existing reproducer; it does not constitute an independently authored test or a security-impact assessment. See `cases/33537/evidence-report.md`.

## Second open-report validation: #33450

**Reproduced and locally patched on 2026-11-05.** The supplied reproducer triggered UBSan on pinned current master; a local guard returned equality (`0`) without a diagnostic for two equal groups that lack generators. The issue's cited short revision was not resolvable, so this was not a historical-revision test. The candidate change is not upstream-reviewed, and broader regression coverage remains future work. See `cases/33450/evidence-report.md`.

## Milestone 2: reusable case workflow

- Import public issue metadata from GitHub without credentials where possible.
- Store claims, revisions, contract references, reproduction commands, and run artifacts in a versioned case directory.
- Require a human-reviewed case manifest and explicit opt-in before executing any case-defined command.
- Generate Markdown and JSON reports from collected evidence.

## Later, only after the workflow proves useful

- Suggest related public reports for human deduplication.
- Draft candidate reproducers for review.
- Add a model adapter only as an optional analysis aid; model outputs remain hypotheses and never change evidence status.

## Out of scope

Automated vulnerability discovery, private security-report access, auto-triage labels, posting comments, closing issues, and submitting changes.
