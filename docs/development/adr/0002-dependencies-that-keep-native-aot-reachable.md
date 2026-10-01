# 2. Dependencies that keep a native-AOT build reachable

- **Status:** Accepted
- **Decided:** 2026-07-16 (#42, #43)
- **Recorded:** 2026-09-21 (#216)
- **Amended:**
  - 2026-09-29 (#268): the number of platforms in the context is eight, not six.

## Context

Chartula ships as a self-contained single-file binary for eight platforms, and a native-AOT build is a goal.
It needs two things from outside: the commits between two tags, and the merged pull requests behind them.

LibGit2Sharp brings a native library per platform, which works against a single trimmed binary.
Octokit covers the whole GitHub API with reflection-based serialization, which is what trimming struggles with most.
Chartula uses a handful of git commands and a few GitHub endpoints.

## Decision

Chartula takes no dependency that brings a native library or relies on reflection-based serialization:

- Commits are read by running the `git` CLI (`GitCliCommitReader`, behind `IReleaseCommitReader`).
- GitHub is read and written with `HttpClient` and source-generated `System.Text.Json` (`GitHubJson.cs`), behind `IReleasePullRequestReader` and `IReleaseNotesWriter`.
- Regexes are source-generated (`[GeneratedRegex]`) for the same reason.

## Consequences

- The binary carries no native library besides the runtime, and trimming has nothing reflection-based to guess at.
- `git` must be installed and on `PATH` where Chartula runs; every checkout that has a repository to release already has it.
- Each GitHub endpoint Chartula uses is written and tested by hand, error mapping included.
  The surface is small enough that this costs less than the dependency.
- Raise it in an issue before adding a package that relies on reflection or a native library.
