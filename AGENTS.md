# AGENTS Guide for Files Repo

This file gives Codex repository-specific instructions to reduce mistakes and speed up useful changes.

## Repo Snapshot

- Stack: .NET + C# + XAML (Windows app)
- Solution: `Files.slnx`
- Main app project: `src/Files.App/Files.App.csproj`
- Tests: `tests/Files.InteractionTests/Files.InteractionTests.csproj`
- Primary CI workflow: `.github/workflows/ci.yml`

## Operating Rules

- Keep changes scoped to the task. Avoid broad refactors unless requested.
- Prefer fixing root causes over suppressing warnings.
- Follow existing patterns in nearby files (naming, null handling, async usage, UI conventions).
- Do not modify release workflows, packaging scripts, or signing steps unless explicitly asked.
- When touching UI behavior, preserve accessibility and keyboard behavior where possible.

## Build and Validation Commands

Run from repo root:

```powershell
msbuild .\Files.slnx -t:Restore -p:Platform=x64 -p:Configuration=Debug -v:quiet
msbuild .\src\Files.App\Files.App.csproj -t:Build -p:Platform=x64 -p:Configuration=Debug -p:AppxBundle=Never -p:GenerateAppxPackageOnBuild=false -v:quiet
```

If needed for release-path validation:

```powershell
msbuild .\src\Files.App\Files.App.csproj -t:Build -p:Platform=x64 -p:Configuration=Release -p:AppxBundle=Never -p:GenerateAppxPackageOnBuild=false -v:quiet
```

## Testing Guidance

- Prefer targeted tests for changed areas first.
- Interaction tests are under `tests/Files.InteractionTests`.
- If local interaction tests are skipped (for environment/tooling reasons), state that clearly in the final report.

## Formatting Guidance

- Respect `.editorconfig` and existing file formatting.
- XAML formatting is enforced in CI (`XamlStyler.Console`).
- Avoid unrelated formatting-only changes in files not touched by the task.

## Pull Request Expectations

- Link the target issue in the PR description.
- Include a concise "what changed" and "why" summary.
- Include validation notes (build/test commands run, and results).
- Keep one logical change per PR.

## High-Risk Areas (Ask Before Changing)

- `.github/workflows/*`
- Packaging/signing scripts in `.github/scripts/*`
- App packaging parameters (`Appx*`, signing certificate handling, installer flow)

## Suggested Codex Workflow

1. Read nearby files for local conventions.
2. Implement minimal code changes.
3. Run at least a targeted build or tests.
4. Report what was validated and any remaining risk.

