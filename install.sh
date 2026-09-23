#!/bin/sh
# Installs Chartula on Linux and macOS:
#
#   curl -fsSL https://raw.githubusercontent.com/goldbarth/chartula/main/install.sh | sh
#
# It picks the binary for this machine - glibc or musl (Alpine) on Linux - checks
# it against the release's SHA256SUMS, and puts it on the PATH as `chartula`.
# Nothing else on the system is changed: a missing library or a missing PATH entry
# is named with the line that fixes it, never fixed by the script.
#
# Settings, all optional:
#   CHARTULA_VERSION      a release tag such as v0.1.0-preview.1 (default: the latest release)
#   CHARTULA_INSTALL_DIR  where the binary goes (default: ~/.local/bin)
#   CHARTULA_DOWNLOAD_URL a directory holding the binaries and SHA256SUMS, instead of
#                         the GitHub release (a mirror, or a local copy for testing)

set -eu

REPO="goldbarth/chartula"
INSTALL_DIR="${CHARTULA_INSTALL_DIR:-$HOME/.local/bin}"

fail() {
    printf 'Error: %s\n' "$1" >&2
    exit 1
}

say() {
    printf '%s\n' "$1"
}

download() {
    # $1 url, $2 target file
    if command -v curl >/dev/null 2>&1; then
        curl -fsSL "$1" -o "$2"
    elif command -v wget >/dev/null 2>&1; then
        wget -q "$1" -O "$2"
    else
        fail "neither curl nor wget is installed; install one of them and run this again."
    fi
}

platform() {
    case "$(uname -s)" in
        Linux) os=linux ;;
        Darwin) os=osx ;;
        MINGW* | MSYS* | CYGWIN*)
            fail "this is the installer for Linux and macOS. On Windows, run in PowerShell:
  irm https://raw.githubusercontent.com/$REPO/main/install.ps1 | iex" ;;
        *) fail "Chartula has no binary for $(uname -s). Build it from source: https://github.com/$REPO#installation" ;;
    esac

    case "$(uname -m)" in
        x86_64 | amd64) arch=x64 ;;
        aarch64 | arm64) arch=arm64 ;;
        *) fail "Chartula has no binary for the $(uname -m) processor. Build it from source: https://github.com/$REPO#installation" ;;
    esac

    # A shell running under Rosetta reports x86_64 on Apple silicon; the native
    # binary is the one to install there.
    if [ "$os" = osx ] && [ "$arch" = x64 ] && [ "$(sysctl -n hw.optional.arm64 2>/dev/null || true)" = 1 ]; then
        arch=arm64
    fi

    # A glibc binary does not start on musl, and the shell then only says
    # "not found". The loader is the test: ldd is not on every musl system.
    if [ "$os" = linux ] && is_musl; then
        os=linux-musl
    fi

    echo "$os-$arch"
}

is_musl() {
    for loader in /lib/ld-musl-*.so.1; do
        [ -e "$loader" ] && return 0
    done
    ldd --version 2>&1 | grep -qi musl
}

# The musl binary needs the C++ runtime (libstdc++, which brings libgcc), which a
# glibc system always has and Alpine does not. Checked before the download, so a
# run without it changes nothing; installing it is left to the user.
require_libstdcxx() {
    for dir in /usr/lib /lib /usr/local/lib /usr/lib64 /lib64; do
        [ -e "$dir/libstdc++.so.6" ] && return 0
    done

    if command -v apk >/dev/null 2>&1; then
        install_line="apk add libstdc++"
    else
        install_line="install libstdc++ with your package manager"
    fi
    as_root=""
    [ "$(id -u)" = 0 ] || as_root=" (as root)"
    fail "Chartula needs libstdc++, which is not installed. Install it$as_root with:
  $install_line
then run this again. Nothing was installed."
}

newest_tag() {
    # Only for the message: the download itself goes through /releases/latest,
    # a plain web redirect that, unlike the API, has no hourly request limit.
    if command -v curl >/dev/null 2>&1; then
        curl -fsSLI -o /dev/null -w '%{url_effective}' "https://github.com/$REPO/releases/latest" 2>/dev/null \
            | sed -n 's|.*/releases/tag/||p'
    fi
}

sha256() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | cut -d ' ' -f 1
    elif command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | cut -d ' ' -f 1
    else
        fail "neither sha256sum nor shasum is installed, so the download cannot be checked."
    fi
}

# The shell the user typed the install line into. $SHELL is unset in most
# containers; the script's parent is that shell under `curl | sh`, and /proc says
# which one it is.
user_shell() {
    if [ -n "${SHELL:-}" ]; then
        basename "$SHELL"
    elif [ -r "/proc/$PPID/comm" ]; then
        cat "/proc/$PPID/comm"
    fi
}

path_hint() {
    # The file each shell reads when a new terminal opens. An interactive bash that
    # is not a login shell - a terminal on Linux, `docker run -it ... bash` - reads
    # ~/.bashrc and never ~/.profile, so ~/.profile is only for the shells that read
    # nothing else.
    case "$(user_shell)" in
        zsh) rc="$HOME/.zshrc" ;;
        bash) if [ "$(uname -s)" = Darwin ]; then rc="$HOME/.bash_profile"; else rc="$HOME/.bashrc"; fi ;;
        fish) rc="" ;;
        *) rc="$HOME/.profile" ;;
    esac

    say ""
    say "$INSTALL_DIR is not on your PATH yet, so the shell cannot find chartula."
    if [ -z "$rc" ]; then
        say "Add it, for this terminal and new ones, with:"
        say "  fish_add_path $INSTALL_DIR"
    else
        say "Use it in this terminal now with:"
        say "  export PATH=\"$INSTALL_DIR:\$PATH\""
        say "and keep it for new terminals with:"
        say "  echo 'export PATH=\"$INSTALL_DIR:\$PATH\"' >> $rc"
    fi
}

# Everything runs from here, so a download cut off halfway through `curl | sh`
# executes nothing instead of half a script.
main() {
    tmp=$(mktemp -d)
    trap 'rm -rf "$tmp"' EXIT

    rid=$(platform)
    file="chartula-$rid"
    case "$rid" in
        linux-musl-*) require_libstdcxx ;;
    esac

    if [ -n "${CHARTULA_DOWNLOAD_URL:-}" ]; then
        base="${CHARTULA_DOWNLOAD_URL%/}"
        version="${CHARTULA_VERSION:-from $base}"
    elif [ -n "${CHARTULA_VERSION:-}" ]; then
        version="$CHARTULA_VERSION"
        base="https://github.com/$REPO/releases/download/$version"
    else
        version="$(newest_tag)"
        version="${version:-(newest release)}"
        base="https://github.com/$REPO/releases/latest/download"
    fi

    say "Installing Chartula $version ($rid) into $INSTALL_DIR"

    download "$base/$file" "$tmp/$file" || fail "could not download $base/$file."
    download "$base/SHA256SUMS" "$tmp/SHA256SUMS" || fail "could not download $base/SHA256SUMS."

    expected=$(awk -v f="$file" '$2 == f { print $1 }' "$tmp/SHA256SUMS")
    [ -n "$expected" ] || fail "SHA256SUMS has no entry for $file."
    actual=$(sha256 "$tmp/$file")
    [ "$expected" = "$actual" ] || fail "the download of $file does not match SHA256SUMS; nothing was installed. Try again, and if it repeats, report it at https://github.com/$REPO/issues"

    mkdir -p "$INSTALL_DIR"
    chmod +x "$tmp/$file"
    mv "$tmp/$file" "$INSTALL_DIR/chartula"

    "$INSTALL_DIR/chartula" --help >/dev/null 2>&1 \
        || fail "chartula was installed to $INSTALL_DIR/chartula but does not start. Please report it at https://github.com/$REPO/issues"

    say "Installed and checked: $INSTALL_DIR/chartula"

    case ":$PATH:" in
        *":$INSTALL_DIR:"*) ;;
        *) path_hint ;;
    esac

    say ""
    say "Before the first run, Chartula needs two keys in the terminal it runs in:"
    say "  export ANTHROPIC_API_KEY=<key>    from https://console.anthropic.com/settings/keys"
    say "  export GITHUB_TOKEN=<token>       from https://github.com/settings/personal-access-tokens/new"
    say "Then, inside your repository: chartula preview"
    say "More: https://github.com/$REPO#readme"
}

main "$@"
