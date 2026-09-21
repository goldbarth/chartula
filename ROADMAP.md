# Roadmap

What comes next for Chartula, in the order it is planned.
A point links to its milestone once it has issues; until then it is a plan, not work in progress.

## Now: the launch

The alpha is out: binaries for six platforms with a one-command install, the fact base, technical, customer and product renderings, both checks, and the four outputs.
Before Chartula is announced, a first run has to work for someone new to it: errors on the way name their cause and fix, and the default of the thorough check rests on a measurement.

[Milestone: Launch](https://github.com/goldbarth/chartula/milestone/5)

## After the launch, in this order

1. Pull request text as data: delimited and size-limited, with the source of a breaking status marked, edits after the merge detected, and links flagged.
2. Flags in the release-notes draft, and protection against a dirty working tree and an already published release.
3. A GitHub Action that downloads the release binary and verifies its checksum, writing draft release notes only; then the Marketplace.
   [Milestone: GitHub Action](https://github.com/goldbarth/chartula/milestone/6)
4. Reading `.github/release.yml`.
5. `render` from a stored `changelog.json`, and a demo that needs no key.
6. Telling what an author claims apart from what is established, then an author-supplied outcome as a fact source.
7. `openai-compatible` out of experimental.
   [Milestone: Providers](https://github.com/goldbarth/chartula/milestone/7)
8. A Homebrew tap and Scoop; code signing for macOS and Windows.
9. A native-AOT build.
   [Milestone: Native AOT](https://github.com/goldbarth/chartula/milestone/8)

## Parked

Ideas that are not on the roadmap for now, kept so they are not lost: chat webhooks (Discord, Slack, Teams), an embeddable widget, an RSS feed, a NuGet `dotnet tool`, and a showcase repository.
They carry the [`parked`](https://github.com/goldbarth/chartula/issues?q=is%3Aissue+is%3Aopen+label%3Aparked) label.
If one of them matters to you, say so on its issue; that is how a parked idea moves back onto the roadmap.

## Out of scope

Considered and set aside, because each would need something Chartula avoids:

- **Email, SMS or WhatsApp broadcast.** It needs a subscriber list and a delivery service, which Chartula would have to host.
- **Read analytics and feedback buttons.** They need a server to collect events.
- **Scripting-based configuration** (e.g. Lua). The configuration is declarative; a scripting runtime would add weight for little gain.
