# OpenSSL Triage

A local, evidence-first workflow for validating **public** OpenSSL bug reports. The tool records source claims and produces a transparent report; it does not discover vulnerabilities, publish comments, or turn AI output into evidence.

## Current state

The first fixture is public issue [#32897](https://github.com/openssl/openssl/issues/32897). It is now resolved upstream by [commit 8daabd4](https://github.com/openssl/openssl/commit/8daabd4), so this is a historical workflow fixture rather than a new finding. The report labels upstream statements separately from reproductions performed by this project.

## Run

Requires .NET 8 SDK.

```sh
dotnet run --project src/OpenSslTriage.Cli -- list
dotnet run --project src/OpenSslTriage.Cli -- inspect 32897
dotnet run --project src/OpenSslTriage.Cli -- report 32897
```

Run the CLI regression and security tests with `dotnet test tests/OpenSslTriage.Cli.Tests`. They cover path confinement, symlink escapes, report markup/link handling, and Docker build-context allowlists.

`report` writes `cases/<id>/evidence-report.md`; pass `--output <path>` to choose another location.

## Reproduce #32897

Requires Docker, Git, and network access for the initial source fetch and image build. The test container then runs with networking disabled.

```sh
bash cases/32897/reproduce.sh
```

The script builds OpenSSL at the reported and fixed commits, calls the actual callback on the same 16-byte input, runs each `fuzz/provider` binary under libFuzzer, and saves logs and checksums under `cases/32897/evidence/`. Generate or refresh the summary with `dotnet run --project src/OpenSslTriage.Cli -- report 32897`.

## Open report checked: #33537

The public issue was open, with no comments or linked pull request, when checked on 2026-11-05. Its supplied CMS C reproducer reproduced the claimed output on both the issue's pinned revision and current `master` at that check. The evidence and rerun command are in `cases/33537/`.

## Open report reproduced and locally patched: #33450

The supplied `EC_GROUP_cmp()` reproducer triggers a UBSan null-pointer diagnostic on pinned current master. A local candidate guard makes the identical call return `0` for equal groups with no generators. The comparison and limitations are recorded in `cases/33450/evidence-report.md`; this change has not been submitted upstream.

## Evidence rules

- Pin the source revision, build configuration, toolchain, inputs, and commands.
- Keep observations distinct from claims about API contracts and security impact.
- A reproduction passing means the harness ran as expected; it does not by itself prove a defect.
- Mark missing or unrun evidence explicitly. Never invent output or infer that failure to reproduce disproves a report.
- Work only from public issues. Send suspected new vulnerabilities through OpenSSL's private reporting channel.
- Human review is required before any upstream communication. No issue comments or PRs are posted by this tool.

See [MVP plan](docs/MVP.md) for the next implementation steps.
