param([switch]$Demo, [switch]$Initialize, [string]$Python = "")
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot 'RemoteWork.Server')
try {
    $serverPython = Join-Path (Get-Location) '.venv\Scripts\python.exe'
    if (-not (Test-Path -LiteralPath $serverPython)) {
        $candidates = if ($Python) { @($Python) } else { @('py', 'python', (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe')) }
        $selectedPython = $null
        foreach ($candidate in $candidates) {
            try {
                $versionCheck = & $candidate -c 'import sys; print(int(sys.version_info >= (3, 11)))' 2>$null
                if ($LASTEXITCODE -eq 0 -and $versionCheck -eq '1') { $selectedPython = $candidate; break }
            } catch { }
        }
        if (-not $selectedPython) { throw 'Python 3.11 이상을 설치하거나 -Python으로 python.exe 경로를 지정하세요.' }
        & $selectedPython -m venv .venv
        if ($LASTEXITCODE -ne 0) { throw 'Python 3.11 이상을 설치하거나 -Python으로 python.exe 경로를 지정하세요.' }
    }
    & $serverPython -m pip install -r requirements.txt --disable-pip-version-check
    if ($LASTEXITCODE -ne 0) { throw '서버 패키지 설치에 실패했습니다. 인터넷 연결을 확인하세요.' }
    $runArgs = @('run.py')
    if ($Demo) { $runArgs += '--demo' }
    if ($Initialize) { $runArgs += '--init' }
    & $serverPython @runArgs
    if ($LASTEXITCODE -ne 0) { throw '서버 실행에 실패했습니다. 환경 설정과 MySQL 상태를 확인하세요.' }
} finally { Pop-Location }
