param(
    [string]$TargetDll = ""
)

$ErrorActionPreference = "Stop"
$modDir = "D:\Games\steamapps\common\RimWorld\Mods\MaiyaMod02C Rim Digital Life：QuantumNet 量子网络拓展"
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# 1. Assemblies (带重试 + 重命名绕过：读锁文件无法覆盖时先改名腾出路径)
$asmDir = Join-Path $modDir "Assemblies"
New-Item -ItemType Directory -Force -Path $asmDir | Out-Null
if (-not [string]::IsNullOrEmpty($TargetDll) -and (Test-Path -LiteralPath $TargetDll)) {
    $dstDll = Join-Path $asmDir ([System.IO.Path]::GetFileName($TargetDll))
    $copied = $false
    for ($i = 0; $i -lt 10; $i++) {
        try {
            Copy-Item -LiteralPath $TargetDll -Destination $dstDll -Force -ErrorAction Stop
            $copied = $true
            break
        } catch {
            # 覆盖失败（目标被读锁/映射）→ 尝试把旧文件改名腾出路径，再复制
            try {
                if (Test-Path -LiteralPath $dstDll) {
                    Rename-Item -LiteralPath $dstDll -NewName ("RimDigitalLife_QuantumNet.dll.old" + $i) -Force -ErrorAction Stop
                    Copy-Item -LiteralPath $TargetDll -Destination $dstDll -Force -ErrorAction Stop
                    $copied = $true
                    break
                }
            } catch {
                Start-Sleep -Milliseconds 500
            }
        }
    }
    if (-not $copied) {
        Write-Warning "[QuantumNet] 复制 DLL 失败（文件可能被锁定）：$dstDll"
    }
    # 清理历史 .old 残留（读锁解除后自然删除）
    Get-ChildItem -LiteralPath $asmDir -Filter "RimDigitalLife_QuantumNet.dll.old*" -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
}

# 2. Defs
$srcDefs = Join-Path $projectDir "Defs"
$dstDefs = Join-Path $modDir "Defs"
if (Test-Path -LiteralPath $dstDefs) { Remove-Item -LiteralPath $dstDefs -Recurse -Force }
if (Test-Path -LiteralPath $srcDefs) { Copy-Item -LiteralPath $srcDefs -Destination $dstDefs -Recurse -Force }

# 3. About
$srcAbout = Join-Path $projectDir "About"
$dstAbout = Join-Path $modDir "About"
if (Test-Path -LiteralPath $dstAbout) { Remove-Item -LiteralPath $dstAbout -Recurse -Force }
if (Test-Path -LiteralPath $srcAbout) { Copy-Item -LiteralPath $srcAbout -Destination $dstAbout -Recurse -Force }

# 4. Textures
$srcTex = Join-Path $projectDir "Textures"
$dstTex = Join-Path $modDir "Textures"
if (Test-Path -LiteralPath $dstTex) { Remove-Item -LiteralPath $dstTex -Recurse -Force }
if (Test-Path -LiteralPath $srcTex) { Copy-Item -LiteralPath $srcTex -Destination $dstTex -Recurse -Force }

# 5. Languages
$srcLang = Join-Path $projectDir "Languages"
$dstLang = Join-Path $modDir "Languages"
if (Test-Path -LiteralPath $dstLang) { Remove-Item -LiteralPath $dstLang -Recurse -Force }
if (Test-Path -LiteralPath $srcLang) { Copy-Item -LiteralPath $srcLang -Destination $dstLang -Recurse -Force }

Write-Output "[QuantumNet] Deploy done -> $modDir"