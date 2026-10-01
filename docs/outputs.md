# Outputs

A `generate` run writes four files into the directory it runs in, and the release notes on GitHub.
This page shows what each one holds and what each [audience](glossary.md#audience)'s text looks like, so you know what you will get before your first run.
`preview` writes none of them.

## What a run writes

| Output | Written from | Written when |
| --- | --- | --- |
| `CHANGELOG.md` | the technical [rendering](glossary.md#rendering) | the technical audience rendered |
| GitHub release notes | the technical rendering | the technical audience rendered, and the run was not given `--no-publish` |
| `release-<tag>.md` | the customer rendering | the customer audience rendered, with at least one entry |
| `changelog.json` | the [facts](glossary.md#fact) and every rendering | at least one audience rendered |
| `chartula-runs/<time>-<tag>.json` | the facts, the settings and the cost of the run | every `generate` run that reached the model calls |

`generate` renders `technical` and `customer` unless `--audience` names others ([CLI](cli.md#--audience)).
The product rendering has no file of its own: its text is in `changelog.json` alone.

The files land in the directory you run `chartula` from, which is the root of your checkout when you follow [Getting started](getting-started.md).
The file names are fixed, so a CI job or a script can rely on them.

When no audience rendered, for example because every model call failed, Chartula writes only the run record.
So a failed run never replaces the files of an earlier, good run.

## Who decides the shape

Chartula, not the model, decides the structure of every rendering: which changes it carries, the heading each entry stands under, the order, the `**Breaking:**` marker and the pull request reference.
The model writes only the words of each entry, and Chartula puts them in place.

The model gets each fact with an id and answers with one text per id.
When an answer leaves a fact without a text, or writes one for a fact it was not sent, the audience fails instead of being written ([The answer does not match the facts](troubleshooting.md#the-answer-does-not-match-the-facts)).

Format rules in the prompt were not enough: five renderings of one release by four models came back in five structures ([#96](https://github.com/goldbarth/chartula/issues/96)).
So a rendering has the same groups and markers whichever model wrote it, and a tool can read its headings.

## The technical rendering

The reader is a developer who works with the source.
The rendering follows [Common Changelog](https://common-changelog.org), a stricter form of Keep a Changelog.

**Groups**, in this order, each only when it has an entry:

| Group | The changes in it |
| --- | --- |
| `### Changed` | every change that reaches the rendering and is neither a `Feature` nor a `Fix` |
| `### Added` | `Feature` |
| `### Fixed` | `Fix` |

`Changed` comes first because it holds changes to what the reader already depends on.
There is no `Removed` group, because nothing in a pull request marks a change as a removal, and Chartula assigns no group it has no source for.
So a removal stands under the group of its category.

**Entries** are one line each.
A breaking change stands first in its group and starts with `**Breaking:**`; after it, the entries follow the category order of `categories.order` ([Configuration](configuration.md#categories)).
Each entry ends with a link to its pull request, which Chartula adds after the model's text.
A change from a commit without a pull request has no link, because there is nothing to link to.
The model is told to open each entry on a verb in the imperative, such as "Add" or "Fix", and to keep the names of files, options and types in backticks, because this reader meets them in the source.

[What goes into a release](what-goes-into-a-release.md#7-who-can-meet-a-change) says which changes reach this rendering, and [the category table](what-goes-into-a-release.md#4-the-category) which category a title gets.

An excerpt of a real run on the `v1.9.0` release of ServiceDeskLite, the release of the [model comparison](costs-and-checks.md#the-model-comparison-of-2026-09-25), rendered by `gpt-6-sol`, with at most two entries of each group:

```markdown
## 1.9.0 - 2026-07-14

### Changed

- Integrate the M9 Frontend Redesign into `main` and add v1.9.0 release notes. ([#244](https://github.com/goldbarth/ServiceDeskLite/pull/244))

### Added

- Add inline citation badges to sanitized assistant Markdown answers. ([#245](https://github.com/goldbarth/ServiceDeskLite/pull/245))
- Add a keyboard-operated `CommandPalette` available on every page through Ctrl/Cmd+K. ([#236](https://github.com/goldbarth/ServiceDeskLite/pull/236))

### Fixed

- Fix the ticket queue `Category` header width so it no longer overlaps `Due`. ([#238](https://github.com/goldbarth/ServiceDeskLite/pull/238))
- Render ticket summary timestamps in `Anthropic:UserTimeZone`. ([#222](https://github.com/goldbarth/ServiceDeskLite/pull/222))
```

### `CHANGELOG.md`

Chartula adds the technical rendering at the top of `CHANGELOG.md`, under a heading of its own, `## VERSION - DATE`:

- **The version** is the tag without a `v` in front of a digit, so `v1.9.0` becomes `1.9.0` and `release-2026` stays as it is.
- **The date** is the day the tag was created, or for a lightweight tag the day of its commit, not the day of the run.
  So a run repeated a week later still dates the release the same.
  When git gives no date, the heading has none, rather than today's.

Everything else in the file stays as it was, including text above the first release, such as a note on how the changelog is made.
A file that does not exist yet starts with `# Changelog`.
A second run for the same release replaces that release's section where it stands, instead of adding another, and pays for its model calls again.

`CHANGELOG.md` is written, not drafted.
Read it, edit it where needed, and commit it the way you commit any other change.

### The GitHub release notes

The release notes are the technical rendering without the `## VERSION - DATE` heading, because GitHub shows the release's own title above them.
A tag without a release gets a new [draft](glossary.md#draft); [What `generate` writes to GitHub](github.md#what-generate-writes-to-github) says what happens to a release that exists already.
`--no-publish` leaves GitHub alone and still writes the files.

## The customer rendering

The reader uses the product and never reads its source.
The rendering groups the changes by what the reader has to do about them, not by the kind of change: someone reading release notes wants to know first whether anything is expected of them.

**Groups**, in this order, each only when it has an entry:

| Group | The changes in it |
| --- | --- |
| `### What needs action` | a breaking change, and a change with a label from `labels.actionRequired` ([Configuration](configuration.md#labels)) |
| `### What's New` | `Feature` |
| `### What's Changed` | every other change that reaches the rendering, such as `Performance` or `Other` |
| `### Bug Fixes` | `Fix` |

"What's Changed" holds improvements as well, because for this reader "better than before" and "different from before" raise the same question: did something I rely on change?

**Entries** start with a bold label the model writes, a few words naming the subject, such as `**Command palette:**`.
A breaking change carries `**Breaking:**` as its label instead, and stands first in "What needs action".
After that, the entries of a group follow `categories.order`, like the technical ones.

The model is told to write each entry in two sentences: what the reader can observe, who it applies to, what they can now rely on, and what they have to do, leaving out a part the facts give nothing for.
It is told never to write pull request numbers, commit hashes, issue references, author names, configuration keys or file paths, because this reader meets none of them.

Only a change a reader can meet reaches this rendering, so an `internal` label keeps a change out of it ([Who can meet a change](what-goes-into-a-release.md#7-who-can-meet-a-change)).

### `release-<tag>.md`

The customer rendering is written as a page of its own, one file per release, ready to publish on a website:

```markdown
---
title: Release 1.9.0
description: This release brings together changes to ticket workflows, assistant answers, and the web interface.
publishedAt: 2026-07-14
---

### What's New

- **Suggested ticket actions:** The first actionable suggestion on a ticket is now a button or agent picker, with other suggestions in a menu. You can act on a suggestion directly, while the usual ticket controls remain available.
- **Command palette:** Press Ctrl+K or Cmd+K from any page to open a palette for navigation and ticket search. You can use the arrow keys and Enter to open a result, or Esc to close the palette and return to your previous focus.

### What's Changed

- **Frontend redesign release:** The frontend redesign changes are now brought together for v1.9.0. You can use the combined release with Markdown assistant answers and a Sources list below them.

### Bug Fixes

- **Board card titles:** Long ticket titles on board cards could grow beyond two lines and stretch the card. Titles now stop at two lines with an ellipsis, so you can count on cards staying bounded.
- **Ticket summary times:** Times in streamed ticket summaries could differ from the local times shown on the ticket. Summaries now use the user timezone, so you can count on their timestamps matching the ticket detail view.
```

This is an excerpt of the same run as the technical example, with at most two entries of each group.

The YAML front matter at the top is what a static site generator reads:

| Field | Its source | When it is left out |
| --- | --- | --- |
| `title` | `Release` and the version, the tag without a `v` in front of a digit | never |
| `description` | one sentence on what the release is about, written by the model in the same call as the entries, from the same facts | when the facts do not support such a sentence |
| `publishedAt` | the day the tag was created, as in `CHANGELOG.md` | when git gives no date |

A field without a source is left out rather than written empty, because an empty field would read as a statement about the release, such as a release without a date.
The checks read the description together with the entries, so a [claim](glossary.md#claim) in it is flagged like any other ([Costs and checks](costs-and-checks.md#the-two-checks)).

The file name is the tag, with `/`, `\` and any other character your system does not allow in a file name replaced by `-`: the tag `release/2.0` gives `release-release-2.0.md`.
A second run for the same release overwrites the file.

The page is written directly, also with `--no-publish`, since a file on your machine publishes nothing.
A release with no change a customer can meet gets no page, rather than an empty one.

## The product rendering

The reader tracks what shipped from outside the code, such as a product manager, and passes it on to people who were not involved.
So each entry says what changed and why it matters, which the other two renderings do not ask for.

All entries stand under one heading, `### Other`.
Chartula does not have the model sort them into themes, because a theme the facts do not give would be a classification the model invented.
A breaking change stands first while `categories.breakingProminent` is on, the default, and carries no marker; after it, the entries follow `categories.order` ([Configuration](configuration.md#categories)).

The model is told to write each entry in two sentences, what changed and why it matters, and to leave the second out when the facts do not say.
Like a customer entry, it carries no pull request numbers, commit hashes, issue references, author names, configuration keys or file paths, because this reader does not work in the repository either.

The product rendering has no file: its text is in `renderings.product` of `changelog.json`, and in the terminal report of the run.
It renders only when `--audience` names it, so a run without it pays for two renderings instead of three.
Its structure is not part of the [1.0 promise](versioning.md#what-you-can-build-on), because it has neither a file nor an evaluation yet.

An excerpt of a real run on the same release, rendered by `gpt-6-sol` on 2026-10-01, with three of its 17 entries:

```markdown
### Other

- A command palette is now available from every page with Ctrl/Cmd+K, combining navigation and ticket search with keyboard controls. Keyboard users can find a destination, open it, or close the palette and return focus to where they were.
- Ticket status and assignee changes now use inline menus instead of dialogs. Agents can make either change without leaving the ticket, losing their scroll position, or switching tabs.
- Ticket summaries now present created, due, comment, and event times in the user’s timezone. Summary times can be read alongside the local times shown in the ticket detail view without the previous UTC discrepancy.
```

## `changelog.json`

`changelog.json` is one release as data, for a tool that reacts to a release, such as a webhook.
It holds the facts of the release without the pull request descriptions, every rendering of the run as Markdown, and the settings the run was made with.
It is meant to be published: the descriptions stay out because their authors wrote them for reviewers, not for the public.

Every run overwrites it, so it holds the current release only.
One file is one release notification: a consumer that receives it has everything about that release, and nothing it has to tell apart from earlier ones.
To keep a history, store each file where your release pipeline keeps its artifacts.
[`changelog.json` format](changelog-json.md) is the schema.

## The run record

`chartula-runs/<time>-<tag>.json` keeps what a run did and what it cost, and the complete facts it rendered from, pull request descriptions included.
It holds no rendered text, since the texts are in `changelog.json`.
A run whose audiences all failed writes it too, because its tokens were spent all the same.

Each run writes a file of its own and never overwrites an earlier one, so every record can be read and diffed on its own.
The name starts with the time in UTC, such as `20260922T123015Z-v1.2.0.json`, so the files list in the order the runs were made.
A character a file name cannot carry, such as the `/` in `release/1.2`, becomes `-`; two runs in the same second get `-2`, `-3` and so on.

The record is never published or uploaded.
It can hold text from private pull requests, so add `chartula-runs/` to your `.gitignore`; commit it only where the history of runs is the point, as in an evaluation repository.
[Run record](run-record.md) is the schema, and [Comparing runs](costs-and-checks.md#comparing-runs) shows what to do with it.

## A release with nothing to say

A release whose changes reach none of a rendering's readers, such as one with only `chore:` and `docs:` pull requests, costs no model call for that rendering, since there is nothing to rephrase.
Then:

- `CHANGELOG.md` gets the release heading alone, such as `## 1.2.0 - 2026-09-09`, so the changelog still lists the release.
- The release notes on GitHub are empty.
- No `release-<tag>.md` is written.
- `changelog.json` holds an empty text for that rendering.

[What is dropped](what-goes-into-a-release.md#6-what-is-dropped) says which changes leave a release before any rendering.

## What no output contains

The checks' [flags](glossary.md#flag) appear in the terminal and in the run record, and in none of the files above.
Nothing in `CHANGELOG.md`, the customer page or the release notes marks a flagged entry, so read the flags before you publish ([Known limitations](../README.md#known-limitations)).
