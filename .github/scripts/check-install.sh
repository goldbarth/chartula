#!/bin/sh
# Installs Chartula with install.sh the way a user does, then runs a real
# `chartula preview` on a clone of this repository against OpenAI with a dummy key.
# Pass is the endpoint's 401: git read the history, the GitHub API answered over
# TLS and the model call went out - for no tokens. install.yml runs it in fresh
# containers and on the macOS runners; it runs locally too, e.g.
#
#   docker run --rm -v "$PWD:/src:ro" -e PACKAGES='apk add --no-cache git ca-certificates' \
#     alpine:latest /src/.github/scripts/check-install.sh
#
# Settings: PACKAGES, the command that installs what the image lacks (optional);
# CHARTULA_DOWNLOAD_URL or CHARTULA_VERSION, passed on to install.sh; GITHUB_TOKEN.

set -eu

src=$(cd "$(dirname "$0")/../.." && pwd)
work=$(mktemp -d)

fail() {
    printf '::error::%s\n' "$1"
    exit 1
}

if [ -n "${PACKAGES:-}" ]; then
    sh -c "$PACKAGES" > "$work/packages.txt" 2>&1 || { cat "$work/packages.txt"; fail "installing the packages failed."; }
fi

# Alpine has no C++ runtime, which the musl binary needs. install.sh has to say so
# with the line that installs it, and install nothing itself.
if [ -e /etc/alpine-release ]; then
    if sh "$src/install.sh" > "$work/hint.txt" 2>&1; then
        cat "$work/hint.txt"
        fail "install.sh succeeded on Alpine without libstdc++."
    fi
    cat "$work/hint.txt"
    grep -q '^  apk add libstdc++$' "$work/hint.txt" || fail "install.sh did not print 'apk add libstdc++'."
    [ ! -e "$HOME/.local/bin/chartula" ] || fail "install.sh installed chartula although it cannot start."
    apk add --no-cache libstdc++ > /dev/null
fi

sh "$src/install.sh" | tee "$work/install.txt"

# The line the PATH hint prints for this terminal has to be the one that works in it.
if ! command -v chartula > /dev/null 2>&1; then
    line=$(grep '^  export PATH=' "$work/install.txt") || fail "install.sh printed no PATH line for this terminal."
    eval "$line"
fi
command -v chartula > /dev/null 2>&1 || fail "chartula is not on the PATH after the hint's line."

git clone --quiet https://github.com/goldbarth/chartula "$work/chartula"
cd "$work/chartula"

set +e
Chartula__Llm__Provider=openai-compatible \
Chartula__Llm__Model=gpt-5.6-luna \
Chartula__Llm__BaseUrl=https://api.openai.com/v1 \
OPENAI_API_KEY=sk-dummy \
    chartula preview --tag v0.1.0-preview.1 --since 4bc57c8 > "$work/preview.txt" 2>&1
code=$?
set -e
cat "$work/preview.txt"

[ "$code" -eq 1 ] || fail "chartula preview exited with $code, expected 1 (every audience failed on the 401)."
grep -q 'answered 401 Unauthorized' "$work/preview.txt" || fail "chartula preview did not report the model endpoint's 401."
echo "Passed: git, the GitHub API and the model endpoint were all reached."
