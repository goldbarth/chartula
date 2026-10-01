# 5. Refuse rather than guess what the user meant

- **Status:** Accepted
- **Decided:** 2026-09-21 (#198, for #138)
- **Recorded:** 2026-10-01 (#320)

## Context

Chartula runs on input it does not control: a command line, a configuration file, an environment inherited from a shell profile or a CI runner, and a git checkout made by someone else.
Some of that input can be read in more than one way, or would silently have no effect.
A tool can then pick the likelier reading and go on, or stop and ask.

Picking goes wrong in ways nobody sees until later.
Each of these cases ran without a word before it was changed:

| Input | What a silent choice did | Changed in |
| --- | --- | --- |
| A first tag | Rendered the whole history as one release, superseded changes included. | #198 (for #138), #282 |
| A model endpoint on the other provider's API host | Sent the key to that provider, so it had to be rotated. | #235 (for #233) |
| A malformed or misindented `chartula.yaml` | Dropped a key it did not know, so `thorough: false` had no effect. | #238 (for #237) |
| A shallow clone | Read the clone's cut-off history as a first release. | #244 (for #243) |
| `review.enabled: true` | Approved every text without showing it to anyone. | #247 |
| `chartula.yaml` and `chartula.yml` side by side | Read one and ignored the other. | #287 (for #278) |
| An option a command does not take | Ran without it: `--nopublish` published, and `generate --help` started a run. | #301 (for #300) |

The result looked like any other run, so the wrong choice surfaced as a wrong release, a public note, or a credential that had left the machine.
The same lesson came earlier from the checks: a failure that reports success costs more than a failure that stops (#70, #75).

## Decision

When Chartula cannot tell what the user meant, it stops and says so, instead of choosing.

- **It stops before anything is spent:** before the first GitHub request and the first model call, through the path the input belongs to: a usage error for the command line, `Configuration error:` for the settings, and `Error:` or `Stopped:` for what the run finds in the checkout.
- **The message names the cause and the fix,** the setting, option or command that says what was meant:

  ```text
  Unknown option '--nopublish' for generate. Did you mean --no-publish?
  ```

  ```text
  Configuration error: Both /work/my-repo/chartula.yaml and /work/my-repo/chartula.yml exist, and a run reads one configuration file. Keep one: move the settings you want into it and delete the other.
  ```

- **Where the answer is the user's to give and a terminal can give it, Chartula asks.** A first tag or a large range is confirmed at the terminal; without one, as in CI, the run stops and names the option that answers up front (`--yes`, `--since`).
- **It refuses only what it can tell for certain.** Input that has one reading is taken as written, even when unusual: an empty list means no entries (#289), an unknown endpoint host is a proxy or a gateway, an unknown model id is left for the endpoint to judge. A refusal that would block a working setup is worse than the guess it prevents.
- **A setting that suggests an effect it does not have is refused,** not kept as a no-op: an unknown key, an ignored second file, a review mode with no reviewer.

The rule is about what the user meant, not about what the release says.
Whether a sentence of a rendering is backed by the facts is a judgement, and the checks report it as a flag that stops nothing.

## Consequences

- A run that could have produced something stops instead, and a CI job fails until the input says what it means: a typo in an option, a `fetch-depth` left at its default, two configuration files in a checkout.
  That costs a rerun, and the message names what to change.
  A silent choice costs more, and later: tokens spent on the wrong range, notes published that nobody meant, a key to rotate.
- A new input, option or setting comes with its refusals: what can make it ambiguous, what makes it a no-op, and the message for each.
  To apply the rule to a new case, ask in order:
  1. Can this input be read more than one way, or would it have no effect? If not, take it as written.
  2. Can Chartula tell that for certain, or only suspect it? If only suspect it, take it as written; the endpoint or a later check says no if it is wrong.
  3. Is the answer the user's to give at a terminal? Then ask, and without a terminal stop and name the option that answers.
  4. Otherwise stop before anything is spent, and name the cause and the fix in the message.
- A refusal a user meets on the command line or in a configuration file is tested through the built CLI as well as the method that throws ([Testing](../testing.md#chartulaclitests)), so the message and the exit status are what the test holds.
- `chartula doctor` asks what a run asks, through the same code, so it reports most of these refusals before a run meets them.
