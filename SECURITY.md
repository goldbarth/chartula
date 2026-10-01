# Security Policy

Chartula runs inside a checkout with an LLM API key and a GitHub token in its environment, and it reads content that other people wrote: files, pull request text, tags.
That makes the line between repository content and the operator's credentials the part worth protecting, and reports about it are very welcome.

## The trust boundary

[ADR 0003](docs/development/adr/0003-endpoints-and-credentials-from-the-environment.md) records where that line runs, and why.
In short:

- The endpoints, and the names of the variables credentials are read from, come from the environment only; a `chartula.yaml` that sets one is refused.
- A credential is never read from a file, and no output names a credential's value: the run header and `chartula doctor` name the variable, not what it holds.
- An endpoint must use `https`, except on this machine, and a model endpoint on the other provider's API host is refused before the first request.

So no repository content can redirect a credential; only whoever controls the environment can.
A way around any of these is in scope.

## What a run sends and writes

- **To the model provider:** the [facts](docs/glossary.md#fact) of the release, once per audience to write the text and once more per audience for the thorough check, which also gets the text it checks.
  A fact holds the change's title, category and breaking status, and its pull request description unless `factBase.depth` is `title-only` ([How much of each change the model reads](docs/what-goes-into-a-release.md#8-how-much-of-each-change-the-model-reads)).
  No code, no diff and no other credential is sent; against a [local server](docs/providers.md#a-local-server), nothing leaves the machine.
- **To and from GitHub:** [What a run reads and writes](docs/github.md#what-a-run-reads-and-writes) lists every request; only `generate` without `--no-publish` writes, and only the release notes of the tag.
- **On disk:** `changelog.json` is meant to be published and holds no pull request description; the run record in `chartula-runs/` holds the descriptions and stays local ([Outputs](docs/outputs.md)).

## Supported versions

Chartula is in alpha and has no stable release yet.
Security fixes go into `main` and the next preview release; older previews are not patched.

## Reporting a vulnerability

**Please do not open a public issue for a vulnerability.**

Report it privately through GitHub instead: [Report a vulnerability](https://github.com/goldbarth/chartula/security/advisories/new).
Only the maintainer sees the report, and the fix can be prepared in a private fork before anything is public.

A useful report says:

- what an attacker controls (a file in the repository, a pull request description, a tag, an environment variable, a network position)
- what they gain (a credential, a request to a host of their choosing, a program run, a changed release)
- the steps or a minimal repository that reproduces it, and the Chartula version or commit

## What to expect

Chartula is maintained by one person, so these are honest targets rather than guarantees:

- an acknowledgement within 3 working days
- a first assessment, and whether it is accepted as a vulnerability, within 14 days
- a fix or a mitigation plan agreed with you before anything is disclosed

Once a fix is released, the advisory is published with credit to you, unless you prefer not to be named.

## Scope

In scope is anything that lets content Chartula reads, or a setting it did not get from the operator, reach further than it should, for example:

- reading or sending a credential to a place the operator did not choose
- running a program or command that is not the operator's
- writing or publishing somewhere the run was not asked to
- a credential showing up in output, logs or `changelog.json`

Not a vulnerability, but still worth a normal issue:

- an LLM rendering that states something the facts do not support - the faithfulness checks exist for that, and a miss is a bug
- pull request text that steers what the model writes: it reaches the model as it is, a [known limitation](README.md#known-limitations) the roadmap addresses; text that makes Chartula send a credential, run a program or publish elsewhere is in scope
- a vulnerability in a dependency that Chartula does not expose - please report it upstream
- anything that needs the operator's own environment or machine to be compromised first
