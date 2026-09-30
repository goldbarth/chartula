# Versioning

Chartula follows [Semantic Versioning 2.0.0](https://semver.org/).
This page says what a version number tells you, what "preview" and "alpha" mean, when 1.0.0 ships, and which parts of Chartula you can build on.
How a release is made is in [Releasing](development/releasing.md).

## The version scheme

```text
MAJOR.MINOR.PATCH[-preview.N]
```

| Part | Increases when |
| --- | --- |
| `MAJOR` | A change breaks a [stable surface](#what-you-can-build-on), from 1.0.0 on. |
| `MINOR` | A feature is added without breaking a stable surface. |
| `PATCH` | A bug is fixed without changing behavior anyone correctly relies on. |
| `-preview.N` | A build before 1.0.0. `N` counts up by one with every release. |

Each part resets the ones to its right: a MINOR release resets PATCH to 0, and a MAJOR release resets MINOR and PATCH.
The version is always written in three parts (`1.0.0`, not `1.0`), and a tag is the version with a `v` in front (`v0.1.0-preview.3`).
So a version number sorts the way it reads, and `preview.10` comes after `preview.9`.

`chartula --version` prints the version with the commit it was built from, `0.1.0-preview.3+<commit>`, and a run records the same text as `toolVersion`.
The tag, the release and the name Chartula sends to GitHub carry the version without the commit.
So a bug report and a run file name the exact build, and a tag names the release.

## Before 1.0.0: preview and alpha

Every release before 1.0.0 is `0.1.0-preview.N`.
The `0.y.z` range is SemVer's range for initial development, where anything may change, and that is where Chartula is: options, the shape of each rendering and what `generate` publishes can still move with the feedback of the alpha.
A `1.0.0-preview` would promise that the shape of 1.0 is known, and every correction would then cost a `2.0.0`.

- **`0.1.0` stays fixed.** Before 1.0.0 the three parts carry no meaning, so a change to them would suggest a distinction that is not there.
- **`N` counts up with every release**, a feature or a fix alike, and never resets before 1.0.0.
- **"Preview" is the label in the version.** It says "not yet stable" wherever the version appears.
- **"Alpha" is the phase in prose.** The project calls the time before 1.0.0 its alpha, and the version does not repeat it.

A preview may break something that worked in the preview before it.
Its release notes say so, the way every rendering marks a breaking change ([Breaking changes](#breaking-changes)).
So read the notes of every preview before you update a CI job that depends on Chartula.

## When 1.0.0 ships

1.0.0 ships when all three of these hold, not on a date:

- The known bugs that block day-to-day use are fixed.
- The stable surfaces have gone through at least one preview without a breaking change.
- Chartula has run on real repositories other than its own.

Until then, the next release is another `preview.N`.
From 1.0.0 on, a breaking change needs a MAJOR release, a migration note in its release notes, and, where it can, a warning in the release before it.
So from 1.0.0 on, a MINOR or PATCH update never breaks what you built on the surfaces below.

## What you can build on

These surfaces are stable from 1.0.0 on, and change only in a MAJOR release:

| Surface | What is covered | Reference |
| --- | --- | --- |
| The CLI | Commands, options, their defaults, and the exit status. | [CLI](cli.md) |
| The configuration | Every key of `chartula.yaml`, its default and its valid values, and the same settings as environment variables. | [Configuration](configuration.md) |
| `changelog.json` | Its schema, versioned by `schemaVersion`. | [`changelog.json` format](changelog-json.md#stability) |
| The run record | Its schema, versioned by its own `schemaVersion`. | [Run record](run-record.md#stability) |
| File names | `CHANGELOG.md`, `release-<tag>.md`, `changelog.json` and `chartula-runs/`, and where a run writes them. | [`chartula generate`](cli.md#chartula-generate) |
| The technical and customer renderings | Their structure: the groups, their order, the markers and the references around each entry. | [`chartula generate`](cli.md#chartula-generate) |

The wording inside an entry is not a surface: the model writes it, and it changes with the model and the release.
The product rendering is not covered either, because it has no output file of its own and no evaluation yet.
So a tool may read the groups of `CHANGELOG.md` or the fields of `changelog.json`, but should not match the text of an entry.

Before 1.0.0 these surfaces can change in any preview.
The two schemas already carry a `schemaVersion`, which rises with every breaking change to them, so a consumer can refuse a file it does not know.

## Breaking changes

A pull request marks a change as breaking with a `!` after its type, or a `BREAKING CHANGE:` footer ([What goes into a release](what-goes-into-a-release.md)).
The renderings put it where a reader meets it first:

- **Technical** (`CHANGELOG.md` and the release notes): the entry stands first in its group and starts with `Breaking:`.
- **Customer** (`release-<tag>.md`): the entry stands under "What needs action", the first group of the page, and starts with `Breaking:`.
- **Product**: the entry stands first when `categories.breakingProminent` is on, the default, and carries no marker ([Configuration](configuration.md#categories)).

Chartula's own release notes are generated by Chartula, so a breaking change in Chartula reaches you the same way.
So the first entries of a group are the ones to read before you update.
