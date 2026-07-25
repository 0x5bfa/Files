# Testing and performance

Files.Core has three validation layers. Tests avoid WinUI and exercise the
model graph through public contracts.

## Unit tests

`tests/Files.Core.Tests` covers:

- lazy capability resolution, composition, ownership, and async disposal;
- thumbnail cache invalidation races and LRU behavior;
- stream preview ownership, blocking, cancellation, and size limits;
- browse navigation, replacement, incremental folder changes, selection,
  projection, preview, and viewport prefetch;
- application/window/tab/pane ownership and navigation history;
- awaited item/capability/CoreModel disposal and aggregated cleanup failures;
- unbuilt-builder cleanup, ownership transfer, construction failure, and
  runtime disposal;
- operation routing, enum validation, and result/progress invariants;
- archive path safety, backend fallback, encryption routing, credential
  retry, and logical parent navigation;
- storage identity and recovery-address equality.

Use test doubles for deterministic model behavior. A test that creates an
`IStorableModel` owns it unless ownership is transferred to a session.

## Windows integration tests

Windows tests use real temporary files and real Shell APIs for:

- item resolution and stable filesystem identity;
- folder enumeration and streams;
- thumbnail PNG extraction;
- typed property extraction;
- Shell scheduler apartment and concurrency behavior;
- `SHChangeNotifyRegister` folder notifications;
- create, rename (including case-only spelling changes), copy, move, and
  permanent delete;
- preview association and session orchestration with controller doubles.

Shell integration tests are marked `DoNotParallelize` where they share
process-level Shell behavior. Every test creates a unique temporary directory
and removes it in `finally`.

The Windows Shell preview controller has a separate manual smoke boundary:
run Files.App's host adapter against representative local `.txt`, `.pdf`, and
Office files because installed third-party handlers and out-of-process COM
servers cannot be made deterministic on a hosted test machine.

Archive scenario tests should keep small committed fixtures for
unencrypted/encrypted ZIP and 7z, a header-encrypted 7z, synthesized folders,
case-distinct names, malformed traversal entries, and a non-seekable backing
stream. Run each supported fixture through Windows 10 and the current
Windows 11 image because Shell eligibility is deliberately capability-based,
not OS-version-based. Never place fixture passwords in production telemetry
or error messages.

## Benchmarks

`tests/Files.Core.Benchmarks` measures deterministic architecture overhead:

- cold and cached capability resolution across contributor counts;
- thumbnail cache hit, miss, insertion, and eviction.

Run:

```powershell
dotnet run --project tests/Files.Core.Benchmarks/Files.Core.Benchmarks.csproj `
	-c Release -- --filter '*'
```

The dry smoke configuration is:

```powershell
dotnet run --project tests/Files.Core.Benchmarks/Files.Core.Benchmarks.csproj `
	-c Release -- --smoke
```

Do not mix Shell, disk, network, or provider latency into these
microbenchmarks. Measure those as scenario tests with machine details,
warm/cold cache state, item count, and installed Shell extensions recorded.

## CI

`.github/workflows/files-core-ci.yml` builds and runs the Core tests on
Windows x64 and runs the benchmark smoke job for pushes to `new`, relevant
pull requests, and manual dispatches.

Before committing locally:

```powershell
dotnet build tests/Files.Core.Tests/Files.Core.Tests.csproj `
	-c Release -p:Platform=x64
dotnet test tests/Files.Core.Tests/Files.Core.Tests.csproj `
	-c Release -p:Platform=x64 --no-build
dotnet run --project tests/Files.Core.Benchmarks/Files.Core.Benchmarks.csproj `
	-c Release -p:Platform=x64 -- --smoke
git diff --check
```

The Core project treats warnings as errors and enables trimming/AOT
compatibility analysis. A successful test run without the Release build is
not sufficient.
