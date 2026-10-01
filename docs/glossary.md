# Glossary

The terms the other pages use, each with one meaning.
Every page links a term here on its first use, and each entry links the page that explains it in full.

## Audience

A group of readers Chartula writes for: `technical` (developers who work with the source), `customer` (people who use the product) or `product` (people who track what shipped).
Each audience gets its own [rendering](#rendering) of the same [fact base](#fact-base), so the texts cannot disagree about what changed.
`generate` writes `technical` and `customer` unless `--audience` names others.
[Outputs](outputs.md) shows the shape of each.

## Claim

A statement in a [rendering](#rendering): something it says happened or now holds, such as "Search is now faster".
The [rule-based check](#rule-based-check) and the [thorough check](#thorough-check) read every claim against the [facts](#fact).
A claim they cannot back becomes a [flag](#flag).

## Draft

A GitHub release that is not published yet.
Only people with write access to the repository see it, so its notes can be read and edited before anyone else does.
`generate` creates a draft for a tag that has no release yet; a release that exists keeps its state ([What `generate` writes to GitHub](github.md#what-generate-writes-to-github)).
`CHANGELOG.md` and the customer page are written directly, not as drafts.

## Fact

One change of a release, as Chartula establishes it without a model: its title, pull request number and link, category, whether it is breaking, whether a reader can meet it, its labels, the issue numbers it closes, and its description.
For example, the pull request `feat!: rename the configuration file` becomes a fact with the category `Feature` that is breaking.
The model only rephrases facts; it never decides one.
[What goes into a release](what-goes-into-a-release.md) explains how each part is decided.

## Fact base

All [facts](#fact) of one release, which every [rendering](#rendering) is written from.
`factBase.depth` decides how much of each change the model reads: the title alone, or the title and the description ([How much of each change the model reads](what-goes-into-a-release.md#8-how-much-of-each-change-the-model-reads)).
`preview` shows the fact base before any model call, and the [run record](run-record.md) keeps it after the run.

## Flag

A [claim](#claim) that the [rule-based check](#rule-based-check) or the [thorough check](#thorough-check) could not back with the [facts](#fact).
A run prints its flags under "Flagged for review", below the rendering they belong to, and keeps them in the [run record](run-record.md).
A flag does not change the text and does not fail the run, so read the flags before you publish: no output file marks a flagged entry.

## Provenance

What a run was made with: the Chartula version, the provider and model, the thinking mode, the [fact base](#fact-base) depth, a hash of the instructions, and the settings of the [thorough check](#thorough-check).
`changelog.json` and the run record both carry it, so two runs can be compared by what made them rather than by memory ([Provenance](changelog-json.md#provenance)).

## Range

The commits a release consists of: those after the previous tag, up to and including the release tag.
For `v1.3.0` after `v1.2.0`, the range is every commit `v1.3.0` has that `v1.2.0` does not.
`--since` starts it after another tag or commit, and the range of a first tag starts at the first commit ([The range](what-goes-into-a-release.md#1-the-range)).

## Rendering

The text of one [audience](#audience), written by the model from the [fact base](#fact-base) and put in shape by Chartula: the technical rendering, the customer rendering, the product rendering.
Chartula decides which changes a rendering carries, their groups, order and markers; the model writes only the words of each entry.
[Outputs](outputs.md) shows each rendering and the files it is written to.

## Rule-based check

The free check of every [rendering](#rendering): it makes no model call and flags a number, or a quoted or backticked name, that no [fact](#fact) contains.
It always runs, and it catches only what a lookup can decide ([The two checks](costs-and-checks.md#the-two-checks)).

## Thorough check

The paid check of every [rendering](#rendering): one more model call that asks whether each [claim](#claim) is backed by the [facts](#fact).
It catches what a lookup cannot, such as a claim of degree or a distorted meaning.
It is on by default, and `faithfulness.thorough: false` turns it off ([Does the thorough check earn its tokens?](costs-and-checks.md#does-the-thorough-check-earn-its-tokens)).
