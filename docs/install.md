# Install

Chartula is one self-contained file per platform.
It needs no .NET, only git, which it reads the release's history with.

## Platforms

| Platform | Binary | Needs |
| --- | --- | --- |
| Linux with glibc, x64 | `chartula-linux-x64` | git, `ca-certificates` |
| Linux with glibc, arm64 | `chartula-linux-arm64` | git, `ca-certificates` |
| Linux with musl (Alpine), x64 | `chartula-linux-musl-x64` | git, `ca-certificates`, `libstdc++` |
| Linux with musl (Alpine), arm64 | `chartula-linux-musl-arm64` | git, `ca-certificates`, `libstdc++` |
| macOS, Intel | `chartula-osx-x64` | git |
| macOS, Apple silicon | `chartula-osx-arm64` | git |
| Windows, x64 | `chartula-win-x64.exe` | git |
| Windows, arm64 | `chartula-win-arm64.exe` | git |

Chartula reaches GitHub and the model provider over HTTPS, so a Linux system needs the CA certificates to trust them.
Desktop distributions ship them; slim container images often do not, so install `ca-certificates` there.
The musl binary also needs the C++ runtime, which Alpine leaves out.

Every release carries these eight binaries, a `SHA256SUMS` file with the checksum of each, and a build provenance attestation per binary.
The attestation proves that a binary was built from this repository by its release workflow, not uploaded by hand.

## With the install script

The script picks the binary for your machine, checks it against `SHA256SUMS`, and puts it where your terminal finds it.
It changes nothing else on your system.

**Linux and macOS**, in a terminal:

```bash
curl -fsSL https://raw.githubusercontent.com/goldbarth/chartula/main/install.sh | sh
```

It installs to `~/.local/bin/chartula`, and picks the musl binary on Alpine and the native binary on Apple silicon.
When that folder is not on your `PATH` yet, the script prints the line that adds it for this terminal and the line that keeps it for new ones.
When `libstdc++` is missing on Alpine, it prints `apk add libstdc++` and installs nothing.
It fixes neither itself, so no file of yours changes without you.

**Windows**, in PowerShell:

```powershell
irm https://raw.githubusercontent.com/goldbarth/chartula/main/install.ps1 | iex
```

It installs to `%LOCALAPPDATA%\Programs\chartula\chartula.exe` and adds that folder to your user `PATH`, never the machine's.
The current PowerShell window finds `chartula` right away, and so does every window opened afterwards.

Both scripts start the installed binary once before they report success, so a binary that cannot run on your machine is reported at install time, not at the first release.
Check it yourself with:

```bash
chartula --help
```

To read a script before running it, open it in a browser: [install.sh](../install.sh), [install.ps1](../install.ps1).

### Settings

Three environment variables change what the scripts do.
All are optional.

| Variable | Default | Effect |
| --- | --- | --- |
| `CHARTULA_VERSION` | the latest release | Installs this release tag instead, such as `v0.1.0-preview.3`. |
| `CHARTULA_INSTALL_DIR` | `~/.local/bin`; on Windows `%LOCALAPPDATA%\Programs\chartula` | Installs into this folder instead. |
| `CHARTULA_DOWNLOAD_URL` | the GitHub release | Downloads from this folder instead, which must hold the binaries and `SHA256SUMS`: a mirror, or a local copy. |

A pinned version keeps a CI job on the release you tested, so a new release cannot change its output unannounced:

```bash
curl -fsSL https://raw.githubusercontent.com/goldbarth/chartula/main/install.sh | CHARTULA_VERSION=v0.1.0-preview.3 sh
```

```powershell
$env:CHARTULA_VERSION = 'v0.1.0-preview.3'; irm https://raw.githubusercontent.com/goldbarth/chartula/main/install.ps1 | iex
```

### Alpine and Docker

An Alpine image has neither git nor curl nor `libstdc++`, and its `PATH` does not include `~/.local/bin`.
Install what is missing and put the folder on the `PATH` yourself, since the script only prints how:

```sh
apk add --no-cache git ca-certificates libstdc++
wget -qO- https://raw.githubusercontent.com/goldbarth/chartula/main/install.sh | sh
export PATH="$HOME/.local/bin:$PATH"
```

A slim Debian or Ubuntu image needs the same, from its own package manager:

```sh
apt-get update && apt-get install -y --no-install-recommends git ca-certificates curl
curl -fsSL https://raw.githubusercontent.com/goldbarth/chartula/main/install.sh | sh
export PATH="$HOME/.local/bin:$PATH"
```

## Download by hand

Download the binary for your platform and `SHA256SUMS` from the [latest release](https://github.com/goldbarth/chartula/releases/latest).
Check the checksum before you run the file, because a download that was cut off or altered fails the check.

**Linux:**

```bash
sha256sum -c SHA256SUMS --ignore-missing
chmod +x chartula-linux-x64
mv chartula-linux-x64 ~/.local/bin/chartula
```

**macOS**, which has `shasum` instead of `sha256sum`:

```bash
grep chartula-osx-arm64 SHA256SUMS | shasum -a 256 -c
chmod +x chartula-osx-arm64
mv chartula-osx-arm64 ~/.local/bin/chartula
```

**Windows**, in PowerShell: compare the hash with the line for your file in `SHA256SUMS`, then rename the file to `chartula.exe` and move it to a folder on your `PATH`.

```powershell
(Get-FileHash chartula-win-x64.exe -Algorithm SHA256).Hash
```

To verify the attestation as well, use the [GitHub CLI](https://cli.github.com/):

```bash
gh attestation verify ~/.local/bin/chartula --repo goldbarth/chartula
```

### Gatekeeper and SmartScreen

The binaries are not code-signed.
A file downloaded through a browser carries a mark that makes macOS and Windows warn before its first start.
The install scripts download without a browser, so they trigger neither warning.

- **macOS Gatekeeper** blocks the first start. Clear the mark with `xattr -d com.apple.quarantine ~/.local/bin/chartula`.
- **Windows SmartScreen** warns on the first start. Choose "More info", then "Run anyway".

## Build from source

Building from source is for contributors, and for a platform without a binary.
It needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) in exactly the version `global.json` names, because the build refuses any other.

```bash
git clone https://github.com/goldbarth/chartula.git
cd chartula
dotnet build Chartula.slnx -c Release
```

The CLI is then at `src/Chartula.Cli/bin/Release/net10.0/chartula`.
To call it as `chartula` from anywhere, link it into a folder on your `PATH`:

```bash
ln -sf "$PWD/src/Chartula.Cli/bin/Release/net10.0/chartula" ~/.local/bin/chartula
```

A build from source needs the .NET runtime to run, unlike a release binary.

## Update

Run the install command again.
It replaces the binary with the latest release, or with the release `CHARTULA_VERSION` names.

A binary downloaded by hand is updated the same way it was installed: download the new one and replace the old file.

## Uninstall

Chartula keeps no settings or caches of its own, so removing the binary removes Chartula.
What it wrote into your repositories, such as `CHANGELOG.md`, `changelog.json` and `chartula-runs/`, stays there.

**Linux and macOS:**

```bash
rm ~/.local/bin/chartula
```

**Windows**, in PowerShell, which also takes the folder off your user `PATH`:

```powershell
$dir = Join-Path $env:LOCALAPPDATA 'Programs\chartula'
Remove-Item -Recurse $dir
$path = [Environment]::GetEnvironmentVariable('Path', 'User') -split ';' | Where-Object { $_ -and $_ -ne $dir }
[Environment]::SetEnvironmentVariable('Path', ($path -join ';'), 'User')
```

With `CHARTULA_INSTALL_DIR`, remove the binary from that folder instead.
