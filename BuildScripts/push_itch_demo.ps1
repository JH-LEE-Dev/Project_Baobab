<#
.SYNOPSIS
    itch.io 데모 빌드를 butler로 업로드합니다.

.DESCRIPTION
    SteamPipe의 vdf가 하는 일을 itch 쪽에서 대신합니다. 손으로 명령을 조립하지 마십시오.

    [왜 스크립트인가]
    업로드는 에디터 쪽 안전장치(PlatformConsistencyGuard, BuildOutputSanitizer)가 닿지 않는
    유일한 구간입니다. 폴더와 대상이 한 줄에 함께 적혀 있어야 둘 중 하나만 고치는 실수가 막힙니다.

    실제로 STOVE 빌드가 ITCH_DEMO 폴더에 들어가 있던 적이 있고, 폴더 이름만으로는 알아채지
    못했습니다. 그래서 이 스크립트는 밀기 전에 빌드 안을 열어 정체를 확인합니다.

    [무엇을 검사하는가]
      1. 빌드가 정말 itch 빌드인지      (메타데이터의 세이브 폴더 문자열)
      2. 배포 금지 산출물이 없는지       (심볼·pdb·Assembly-CSharp.dll·steam_api64.dll 등)
      3. 워킹 트리가 깨끗한지            (경고만 - userversion에 박는 커밋 해시와 맞추기 위해)
    하나라도 걸리면 업로드하지 않고 멈춥니다.

.PARAMETER Force
    확인 프롬프트를 건너뜁니다. 검사 자체는 그대로 돕니다.

.EXAMPLE
    .\push_itch_demo.ps1
    .\push_itch_demo.ps1 -Force
#>

[CmdletBinding()]
param(
    [switch] $Force
)

$ErrorActionPreference = 'Stop'

# ===== 설정 =================================================================

# butler 실행 파일. OneDrive 같은 동기화 폴더 아래에 두지 마십시오.
# 파일 온디맨드가 exe나 옆의 DLL을 탈수화하면 실행 자체가 실패합니다.
$ButlerExe = 'C:\butler-windows-amd64\butler.exe'

# 빌드 출력 폴더. PlatformBuildModeSwitcher.BuildFolderName 이 만드는 이름과 같아야 합니다.
$BuildDir = 'C:\Unity Build\ITCH_DEMO'

# 업로드 대상. 페이지 주소 hiddenstagegames.itch.io/lumberboy 에서 온 값입니다.
# 제목(LumberBoy Demo)이 아니라 URL의 이름입니다.
$Target = 'hiddenstagegames/lumberboy'

# 채널 이름이 플랫폼 태그를 결정합니다. windows 가 들어가야 itch 앱이 설치 대상으로 인식합니다.
$Channel = 'windows'

# 버전은 여기 적지 않습니다. ProjectSettings 의 bundleVersion 을 그대로 읽습니다.
# 하드코딩하면 버전을 올렸을 때 실제 빌드와 itch 에 기록되는 값이 조용히 어긋납니다.
# (실제로 1.0.1 빌드를 1.0.0 으로 올릴 뻔했습니다)

# 이 폴더가 어느 스토어의 빌드여야 하는지. 세이브 폴더 이름이 스토어마다 달라 구분자가 됩니다.
$MustContain    = 'LumberBoy_ITCH'
$MustNotContain = @('LumberBoy_STOVE')

# 실려 있으면 안 되는 것들. BuildOutputSanitizer가 이미 지우지만 여기서 한 번 더 봅니다.
# Development Build는 Sanitizer가 통째로 건너뛰므로, 그걸로 뽑은 폴더는 여기서 걸립니다.
$Forbidden = @(
    @{ Pattern = '*BackUpThisFolder*';       Why = 'IL2CPP가 뱉은 C++ 소스' },
    @{ Pattern = '*BurstDebugInformation*';  Why = 'Burst 디버그 정보' },
    @{ Pattern = '*.pdb';                    Why = '디버그 심볼' },
    @{ Pattern = 'Assembly-CSharp.dll';      Why = 'Mono 빌드입니다. 소스가 그대로 나갑니다' },
    @{ Pattern = 'steam_appid.txt';          Why = 'Steam 개발용 우회 파일' },
    @{ Pattern = 'steam_api*.dll';           Why = 'itch 빌드에 Steam DLL' }
)

# ===== 도우미 ===============================================================

function Write-Step   { param([string] $Text) Write-Host ''; Write-Host "== $Text" -ForegroundColor Cyan }
function Write-Ok     { param([string] $Text) Write-Host "   OK   $Text" -ForegroundColor Green }
function Write-Warn   { param([string] $Text) Write-Host "   경고 $Text" -ForegroundColor Yellow }

function Stop-WithReason {
    param([string] $Text)
    Write-Host ''
    Write-Host "중단: $Text" -ForegroundColor Red
    Write-Host '업로드하지 않았습니다.' -ForegroundColor Red
    exit 1
}

<#
    네이티브 exe를 부를 때 ErrorActionPreference를 잠시 풉니다.

    Windows PowerShell 5.1은 네이티브 명령이 stderr에 쓴 줄을 ErrorRecord로 감쌉니다.
    종료 코드가 0이어도 그렇습니다. ErrorActionPreference가 Stop이면 그 순간 스크립트가 죽습니다.

    butler는 버전도 진행률도 stderr로 찍으므로, 이 처리를 빼면 업로드 도중에 멈춥니다.
    실제 성공/실패 판정은 여기가 아니라 호출한 쪽에서 $LASTEXITCODE로 합니다.
#>
function Invoke-Native {
    param([Parameter(Mandatory = $true)] [scriptblock] $Script)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try   { & $Script }
    finally { $ErrorActionPreference = $previous }
}

<#
    ProjectSettings 에서 bundleVersion 을 읽습니다.

    읽지 못하면 멈춥니다. itch 에는 SteamPipe 의 desc 에 해당하는 자리가 없어서 userversion 이
    "이 빌드가 어느 코드였나"를 아는 유일한 단서입니다. 틀린 값으로 올리느니 올리지 않는 편이 낫습니다.
#>
function Get-BundleVersion {
    param([Parameter(Mandatory = $true)] [string] $ProjectRoot)

    $settingsPath = Join-Path $ProjectRoot 'ProjectSettings\ProjectSettings.asset'

    if (-not (Test-Path -LiteralPath $settingsPath)) {
        Stop-WithReason "ProjectSettings.asset 을 찾지 못했습니다: $settingsPath"
    }

    $match = Select-String -LiteralPath $settingsPath -Pattern '^\s*bundleVersion:\s*(.+?)\s*$' | Select-Object -First 1

    if ($null -eq $match) {
        Stop-WithReason "ProjectSettings.asset 에서 bundleVersion 을 읽지 못했습니다: $settingsPath"
    }

    return $match.Matches[0].Groups[1].Value
}

# 바이너리에서 ASCII 문자열을 찾습니다. global-metadata.dat 는 12MB 정도라 통째로 읽어도 됩니다.
function Test-BinaryContains {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [string] $Needle
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $text  = [System.Text.Encoding]::ASCII.GetString($bytes)

    return $text.Contains($Needle)
}

# ===== 1. 도구와 폴더 =======================================================

Write-Step '도구와 폴더'

if (-not (Test-Path -LiteralPath $ButlerExe)) {
    Stop-WithReason "butler를 찾지 못했습니다: $ButlerExe"
}

$butlerVersion = (Invoke-Native { & $ButlerExe -V 2>&1 } | Select-Object -First 1 | ForEach-Object { $_.ToString() })
Write-Ok "butler $butlerVersion"

if (-not (Test-Path -LiteralPath $BuildDir)) {
    Stop-WithReason "빌드 폴더가 없습니다: $BuildDir"
}

$exePath = Join-Path $BuildDir 'LumberBoy.exe'

if (-not (Test-Path -LiteralPath $exePath)) {
    Stop-WithReason "LumberBoy.exe가 없습니다. 빌드가 안 끝났거나 폴더가 비어 있습니다: $BuildDir"
}

$builtAt = (Get-Item -LiteralPath $exePath).LastWriteTime
Write-Ok "빌드 폴더 $BuildDir  (빌드 시각 $builtAt)"

# ===== 2. 이 빌드가 정말 itch 빌드인가 ======================================

Write-Step '빌드 정체 확인'

$metaPath = Join-Path $BuildDir 'LumberBoy_Data\il2cpp_data\Metadata\global-metadata.dat'

if (-not (Test-Path -LiteralPath $metaPath)) {
    Stop-WithReason "메타데이터를 찾지 못했습니다. IL2CPP 빌드가 맞습니까? $metaPath"
}

if (-not (Test-BinaryContains -Path $metaPath -Needle $MustContain)) {
    Stop-WithReason "이 빌드에 $MustContain 이 없습니다. itch 빌드가 아닙니다."
}

Write-Ok "$MustContain 확인"

foreach ($needle in $MustNotContain) {
    if (Test-BinaryContains -Path $metaPath -Needle $needle) {
        Stop-WithReason "이 빌드에 $needle 이 들어 있습니다. 다른 스토어의 빌드입니다. 폴더를 비우고 itch로 다시 빌드하십시오."
    }

    Write-Ok "$needle 없음"
}

# ===== 3. 배포 금지 산출물 ==================================================

Write-Step '배포 금지 산출물'

foreach ($rule in $Forbidden) {
    $hits = @(Get-ChildItem -LiteralPath $BuildDir -Recurse -Force -Filter $rule.Pattern -ErrorAction SilentlyContinue)

    if ($hits.Count -gt 0) {
        Write-Host ''
        foreach ($hit in $hits) { Write-Host "     $($hit.FullName)" -ForegroundColor Red }
        Stop-WithReason "$($rule.Pattern) 이 남아 있습니다 - $($rule.Why)"
    }

    Write-Ok "$($rule.Pattern) 없음"
}

# ===== 4. 버전 문자열 =======================================================

Write-Step '버전'

$projectRoot = Split-Path -Parent $PSScriptRoot
$baseVersion = Get-BundleVersion -ProjectRoot $projectRoot
$userVersion = $baseVersion

Write-Ok "bundleVersion = $baseVersion  (ProjectSettings 에서 읽음)"

try {
    $commit = (Invoke-Native { & git -C $projectRoot rev-parse --short HEAD 2>$null })

    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commit)) {
        $userVersion = "$baseVersion-$($commit.Trim())"

        $dirty = (Invoke-Native { & git -C $projectRoot status --porcelain 2>$null })

        if (-not [string]::IsNullOrWhiteSpace($dirty)) {
            Write-Warn '워킹 트리에 커밋되지 않은 변경이 있습니다.'
            Write-Warn '이 빌드가 아래 커밋과 정확히 같다고 보장할 수 없습니다.'
        }
    }
}
catch {
    Write-Warn "git에서 커밋 해시를 읽지 못했습니다. 버전에 해시를 붙이지 않습니다."
}

Write-Ok "userversion = $userVersion"

# ===== 5. 확인 =============================================================

$destination = "$Target`:$Channel"

Write-Step '업로드 대상'
Write-Host ''
Write-Host "   폴더   $BuildDir"
Write-Host "   대상   $destination"
Write-Host "   버전   $userVersion"
Write-Host ''

if (-not $Force) {
    $answer = Read-Host '올릴까요? (y/N)'

    if ($answer -ne 'y' -and $answer -ne 'Y') {
        Write-Host '취소했습니다.' -ForegroundColor Yellow
        exit 0
    }
}

# ===== 6. 업로드 ============================================================

Write-Step '업로드'

Invoke-Native { & $ButlerExe push $BuildDir $destination --userversion $userVersion }

if ($LASTEXITCODE -ne 0) {
    Stop-WithReason "butler push 가 실패했습니다. (종료 코드 $LASTEXITCODE)"
}

<#
    여기서 butler status 를 바로 부르지 않습니다.

    push 가 끝나도 itch 서버가 빌드를 처리하는 동안에는 채널이 만들어지지 않습니다.
    그래서 업로드 직후의 status 는 거의 항상 "No channel found" 를 돌려주는데,
    성공했는데 실패한 것처럼 보여 사람을 헷갈리게 합니다. (실제로 그렇게 한 번 겪었습니다)

    기다렸다 자동으로 다시 묻는 방법도 있지만, 처리 시간이 빌드 크기와 서버 사정에 따라
    달라서 얼마를 기다려야 할지 정할 수가 없습니다. 명령만 알려주고 사람이 확인하게 둡니다.
#>

Write-Step '다음'

Write-Host ''
Write-Host '  업로드는 끝났습니다. itch 서버가 빌드를 처리하는 데 몇 분 걸립니다.'
Write-Host '  처리가 끝나야 채널이 생기므로, 지금 확인하면 비어 있는 것이 정상입니다.'
Write-Host ''
Write-Host '  잠시 뒤 아래 명령으로 확인하십시오:'
Write-Host ''
Write-Host "    $ButlerExe status $Target" -ForegroundColor Cyan
Write-Host ''
Write-Host '  BUILD 칸에 체크 표시가 뜨면 처리가 끝난 것입니다.'
Write-Host '  그 뒤 itch 페이지에서 파일이 보이는지, 플랫폼이 Windows로 잡혔는지 확인하십시오.'
Write-Host ''
