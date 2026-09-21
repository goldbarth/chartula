# Security Policy

Chartula runs inside a checkout with an LLM API key and a GitHub token in its environment, and it reads content that other people wrote: files, pull request text, tags.
That makes the line between repository content and the operator's credentials the part worth protecting, and reports about it are very welcome.

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
- a vulnerability in a dependency that Chartula does not expose - please report it upstream
- anything that needs the operator's own environment or machine to be compromised first
