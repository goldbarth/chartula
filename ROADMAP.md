# Roadmap

What comes next for Chartula, in the order it is planned.
Each point says what changes for you, and links its issue or milestone; a point without one is a plan, not work in progress.
A point that removes one of the [known limitations](README.md#known-limitations) names it.

## Now: the launch

The alpha is out: binaries for eight platforms with a one-command install, `chartula doctor`, `preview` and `generate`, the technical, customer and product renderings, both checks, and the four outputs.
Before Chartula is announced, a first run has to work for someone new to it, and these points are left:

- The run header says when a run has no GitHub token or no model key, instead of naming a variable that is empty ([#210](https://github.com/goldbarth/chartula/issues/210)).
- "Flagged for review" stands apart from the entries, so it no longer reads as part of the last one ([#211](https://github.com/goldbarth/chartula/issues/211)).
- A decision on whether `changelog.json` writes `+`, quotes and non-ASCII characters as they are, so a person can read and diff it ([#226](https://github.com/goldbarth/chartula/issues/226)).
- A release with nothing for developers says so in `CHANGELOG.md` and its release notes, instead of an empty heading and empty notes ([#307](https://github.com/goldbarth/chartula/issues/307)).
- The documentation covers a first run from install to a published draft, and every page a user needs on the way; this has no issue.

[Milestone: Launch](https://github.com/goldbarth/chartula/milestone/5)

## After the launch, in this order

1. **Pull request text is checked before the model reads it.**
   A breaking status says where it came from, a description edited after the merge is noticed, and a link is flagged until it is checked.
   Removes the limitation "Breaking status comes from the text", and the rest of "Pull request text is quoted, not trusted".
   No issue yet.
2. **A run with flags stops and shows them before it writes anything, and a published release is not changed unasked.**
   A run in a working tree with uncommitted changes stops too.
   Removes the limitations "Flags appear in the terminal only" and "A published release's notes are replaced unasked".
   [#249](https://github.com/goldbarth/chartula/issues/249); the working tree has no issue yet.
3. **A GitHub Action writes the release notes draft on every tag.**
   It downloads the release binary and verifies its checksum, and writes a draft only; the Marketplace listing follows.
   Removes the limitation "No GitHub Action yet".
   [Milestone: GitHub Action](https://github.com/goldbarth/chartula/milestone/6)
4. **A repository that sorts its GitHub release notes with `.github/release.yml` reuses that file.**
   No issue yet.
5. **A release can be rendered again without GitHub, and Chartula can be tried without a model key.**
   Rendering reads an earlier run's stored facts instead of GitHub, and a demo shows the output without a key.
   No issue yet.
6. **An entry tells what its author claims apart from what Chartula established.**
   After that, an author can state a change's outcome for the customer rendering to use.
   No issue yet.
7. **Chartula installs through Homebrew and Scoop, and its binaries are code-signed for macOS and Windows.**
   Removes the limitations "The binaries are not code-signed" and "No package manager".
   No issue yet.
8. **Each binary becomes a native executable that starts faster.**
   [Milestone: Native AOT](https://github.com/goldbarth/chartula/milestone/8)
9. **Chartula reads merge requests from GitLab.**
   Removes the limitation "GitHub is the only source".
   No issue yet.

## Parked

Ideas that are not on the roadmap for now, kept so they are not lost: chat webhooks (Discord, Slack, Teams), an embeddable widget, an RSS feed, a NuGet `dotnet tool`, and a showcase repository.
They carry the [`parked`](https://github.com/goldbarth/chartula/issues?q=is%3Aissue+is%3Aopen+label%3Aparked) label.
If one of them matters to you, say so on its issue; that is how a parked idea moves back onto the roadmap.

## Out of scope

Considered and set aside, because each would need something Chartula avoids:

- **Email, SMS or WhatsApp broadcast.** It needs a subscriber list and a delivery service, which Chartula would have to host.
- **Read analytics and feedback buttons.** They need a server to collect events.
- **Scripting-based configuration** (e.g. Lua). The configuration is declarative; a scripting runtime would add weight for little gain.
