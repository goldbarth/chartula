# 1. Facts are established first, then rephrased

- **Status:** Accepted
- **Decided:** 2026-07-16 (#45); the structure of a rendering 2026-09-16 (#129, for #96)
- **Recorded:** 2026-09-21 (#216)
- **Amended:**
  - 2026-10-01 (#ADRPR): the consequence that the audiences "cannot disagree" is narrowed to what a shared fact base gives them.

## Context

A release note makes claims: this change is in the release, it is a fix, it is breaking, it concerns this issue.
A model asked to write release notes from raw pull requests decides those claims itself, and gets them wrong in ways a reader cannot see: a change that is not in the release, an internal refactor presented as a feature, a breaking change described as a detail.
Once the model has made such a decision, nothing downstream can tell it apart from a correct one.

## Decision

Everything that is a claim about the release is decided without a model and stored as a `ChangeFact` in a `FactBase`: which changes are in it (`Curation`, `Filtering`), how each is categorized (`Categorization`), which labels steer it (`Labeling`), whether it is user-visible or breaking, and which link it gets.

Only then is a model called, once per audience, to rephrase those facts.
Two checks then read each rendering against the facts, and what they cannot back is flagged.

Since #129 (for #96), the structure of a rendering is decided the same way, because format rules in the prompt were not enough: five renderings of one release by four models came back in five structures.
The model is sent the facts with an id each and answers with one text per id.
Code builds the headings, groups, order, breaking markers and references around those texts, and a rendering whose ids do not match the facts one to one fails instead of being written (`RenderingComposer.FindMismatch`).

## Consequences

- The model cannot add or drop a change; it can only phrase one badly, which is what the checks look for.
- All audiences render from the same fact base, so every text starts from the same changes, categories and breaking status.
  It does not stop two texts from wording a change differently enough to disagree: each check reads one text against the facts, not the texts against each other.
- The fact base is testable without a model: the suite replays stored fact bases at no token cost ([Testing](../testing.md#fixtures-stored-fact-bases)).
- A classification, category, flag or link decision never moves into a prompt, even when a prompt would be the quicker change.
- A rendering concern never strips a fact out of the fact base: a fact dropped there cannot be recovered downstream.
  Labels, for example, are carried verbatim, and each rendering decides which it shows.
- The facts are only as true as the pull requests they come from.
  A wrong description stays wrong, and both checks verify against that same text.
