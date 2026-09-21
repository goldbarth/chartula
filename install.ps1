# Installs Chartula on Windows. In PowerShell:
#
#   irm https://raw.githubusercontent.com/goldbarth/chartula/main/install.ps1 | iex
#
# It picks the binary for this machine, checks it against the release's
# SHA256SUMS, installs it as chartula.exe and adds its folder to the user's PATH.
# Editing PATH by hand is where a first install on Windows usually stalls, so the
# script does it; it touches the user's PATH only, never the machine's.
#
# Settings, all optional:
#   $env:CHARTULA_VERSION       a release tag such as v0.1.0-preview.1 (default: the latest release)
#   $env:CHARTULA_INSTALL_DIR   where the binary goes (default: %LOCALAPPDATA%\Programs\chartula)
#   $env:CHARTULA_DOWNLOAD_URL  a directory holding the binaries and SHA256SUMS, instead of
#                               the GitHub release (a mirror, or a local copy for testing)

# Everything runs inside the script block, so a download cut off halfway through
# `irm | iex` executes nothing instead of half a script.
& {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue' # the progress bar slows Invoke-WebRequest down many times over
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    try {
        $repo = 'goldbarth/chartula'
        $failed = 'chartula-install-failed'
        $installDir = if ($env:CHARTULA_INSTALL_DIR) { $env:CHARTULA_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA 'Programs\chartula' }

        function Fail([string] $message) {
            Write-Host "Error: $message" -ForegroundColor Red
            throw $failed
        }

        # The OS architecture, not the process's: a 32-bit or emulated PowerShell
        # would otherwise pick the wrong binary.
        $arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
            'X64' { 'x64' }
            'Arm64' { 'arm64' }
            default { Fail "Chartula has no binary for a $_ processor. Build it from source: https://github.com/$repo#installation" }
        }
        $file = "chartula-win-$arch.exe"

        if ($env:CHARTULA_DOWNLOAD_URL) {
            $base = $env:CHARTULA_DOWNLOAD_URL.TrimEnd('/')
            $version = "from $base"
        }
        elseif ($env:CHARTULA_VERSION) {
            $version = $env:CHARTULA_VERSION
            $base = "https://github.com/$repo/releases/download/$version"
        }
        else {
            # /releases/latest is a plain web redirect; unlike the API it has no hourly request limit.
            $version = '(latest release)'
            $base = "https://github.com/$repo/releases/latest/download"
        }

        Write-Host "Installing Chartula $version (win-$arch) into $installDir"

        $tmp = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
        New-Item -ItemType Directory -Path $tmp | Out-Null
        try {
            try {
                Invoke-WebRequest -UseBasicParsing -Uri "$base/$file" -OutFile (Join-Path $tmp $file)
                Invoke-WebRequest -UseBasicParsing -Uri "$base/SHA256SUMS" -OutFile (Join-Path $tmp 'SHA256SUMS')
            }
            catch {
                Fail "could not download $base/$file ($($_.Exception.Message))."
            }

            $expected = Get-Content (Join-Path $tmp 'SHA256SUMS') |
                ForEach-Object { $parts = $_ -split '\s+'; if ($parts[1] -eq $file) { $parts[0] } } |
                Select-Object -First 1
            if (-not $expected) { Fail "SHA256SUMS has no entry for $file." }
            $actual = (Get-FileHash -Algorithm SHA256 (Join-Path $tmp $file)).Hash
            if ($actual -ne $expected) {
                Fail "the download of $file does not match SHA256SUMS; nothing was installed. Try again, and if it repeats, report it at https://github.com/$repo/issues"
            }

            New-Item -ItemType Directory -Force -Path $installDir | Out-Null
            $target = Join-Path $installDir 'chartula.exe'
            Move-Item -Force (Join-Path $tmp $file) $target
        }
        finally {
            Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
        }

        & $target --help | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Fail "chartula was installed to $target but does not start. Please report it at https://github.com/$repo/issues"
        }
        Write-Host "Installed and checked: $target"

        $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
        $entries = @($userPath -split ';' | Where-Object { $_ })
        if ($entries -notcontains $installDir) {
            [Environment]::SetEnvironmentVariable('Path', (($entries + $installDir) -join ';'), 'User')
            Write-Host "Added $installDir to your PATH. Terminals opened from now on find chartula."
        }
        # This session too, so chartula works right away without a new terminal.
        if (($env:Path -split ';') -notcontains $installDir) {
            $env:Path = "$env:Path;$installDir"
        }

        Write-Host ''
        Write-Host 'Before the first run, Chartula needs two keys in the terminal it runs in:'
        Write-Host '  $env:ANTHROPIC_API_KEY = "<key>"    from https://console.anthropic.com/settings/keys'
        Write-Host '  $env:GITHUB_TOKEN = "<token>"       from https://github.com/settings/personal-access-tokens/new'
        Write-Host 'Then, inside your repository: chartula preview'
        Write-Host "More: https://github.com/$repo#readme"
    }
    catch {
        # Fail has already said what went wrong; anything else is reported here.
        # No exit: under `irm | iex` it would close the user's PowerShell window.
        if ($_.Exception.Message -ne $failed) {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }
        Write-Host 'Chartula was not installed.' -ForegroundColor Red
    }
}
