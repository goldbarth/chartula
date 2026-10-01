# Architecture decision records

An architecture decision record (ADR) keeps a decision that constrains future changes, with the reasons it was taken.
A change that goes against one starts as an issue, not a pull request, so the reasons are weighed before the code moves.

| ADR | Decision | Status | Decided |
| --- | --- | --- | --- |
| [0001](0001-facts-first-then-rephrase.md) | Facts are established first, then rephrased | Accepted | 2026-07-16 |
| [0002](0002-dependencies-that-keep-native-aot-reachable.md) | Dependencies that keep a native-AOT build reachable | Accepted | 2026-07-16 |
| [0003](0003-endpoints-and-credentials-from-the-environment.md) | Endpoints and credential names come from the environment only | Accepted | 2026-09-21 |

## When to write one

Write an ADR when a decision rules out a whole kind of change, such as a dependency, a place where a rule lives, or a setting that must not be in the configuration file.
A decision that only one pull request needs belongs in that pull request's description.

Number a new ADR after the last one, name the file after its decision, and add it to the table above in the same pull request.

## Template

```markdown
# N. The decision, as a statement

- **Status:** Accepted
- **Decided:** YYYY-MM-DD (#issue or pull request that made the decision)
- **Recorded:** YYYY-MM-DD (#pull request that wrote this record)
- **Amended:**
  - YYYY-MM-DD (#pull request): what changed, in one sentence.

## Context

What forced a decision, and what the alternatives would have cost.

## Decision

What holds, in terms a contributor can check a change against.

## Consequences

What the decision makes possible, what it costs, and what a contributor has to do because of it.
```

- **Decided** is when the decision took effect in the code, which can be before the record was written.
- **Recorded** is when this file was written.
- **Amended** lists every later change to the decision or to a fact in the record, oldest first, each with its date and pull request. Leave the field out while there is none.
- A moved file, a fixed link or a reworded sentence is not an amendment.
- **Status** is `Accepted`, or `Superseded by NNNN` when a later ADR replaces it. A superseded ADR stays, so the history of a decision can be read.
