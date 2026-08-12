# ============================================================
# 彩虹时钟 RainbowClock 部署脚本
# 只读使用整合包（E:\Download\...- 副本），仅向 Steam 游戏写入：
#   IPA 覆盖层 + BSML + SiraUtil + RainbowClock
# 用法:  powershell -ExecutionPolicy Bypass -File CopyToGame.ps1 [-Build] [-Launch]
# ============================================================
param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\Beat Saber",
    [string]$PackDir = "E:\Download\Beat Saber 1.40.8 MOD Only Steam 20260117 - 副本",
    [switch]$Build,
    [switch]$Launch
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$ScriptDir = $PSScriptRoot
$ModDll = "$ScriptDir\RainbowClock\bin\Release\RainbowClock.dll"
$ModPdb = "$ScriptDir\RainbowClock\bin\Release\RainbowClock.pdb"

# ---------- 0. 校验路径 ----------
foreach ($p in @($GameDir, $PackDir)) {
    if (-not (Test-Path $p)) { throw "目录不存在: $p" }
}

# ---------- 1. 编译 ----------
if ($Build -or -not (Test-Path $ModDll)) {
    Write-Host "==> dotnet build (Release)"
    Push-Location "$ScriptDir\RainbowClock"
    try {
        dotnet build -c Release --nologo -v m
        if ($LASTEXITCODE -ne 0) { throw "dotnet build 失败" }
    } finally { Pop-Location }
}

# ---------- 2. 安装 IPA 覆盖层（仅首次） ----------
$needIpa = -not (Test-Path "$GameDir\IPA.exe")
if ($needIpa) {
    Write-Host "==> 复制 BSIPA 覆盖层"
    foreach ($item in @("IPA.exe", "IPA.exe.config", "IPA.runtimeconfig.json", "winhttp.dll", "IPA")) {
        $src = "$PackDir\$item"; $dst = "$GameDir\$item"
        if (Test-Path $src) {
            if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
            Copy-Item $src $dst -Recurse -Force
        }
    }
    Write-Host "==> 运行 IPA.exe 安装加载器"
    Push-Location $GameDir
    try {
        & ".\IPA.exe"
        if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) {
            Write-Warning "IPA.exe 退出码: $LASTEXITCODE（若已安装可忽略）"
        }
    } finally { Pop-Location }
} else {
    Write-Host "==> BSIPA 已安装，跳过"
}

# ---------- 3. 复制依赖模组（仅缺失时） ----------
$needDeps = @("BSML.dll", "SiraUtil.dll") | Where-Object { -not (Test-Path "$GameDir\Plugins\$_") }
if ($needDeps) {
    New-Item -ItemType Directory -Force -Path "$GameDir\Plugins" | Out-Null
    foreach ($dll in $needDeps) {
        Write-Host "==> 复制 $dll"
        foreach ($ext in @(".dll", ".pdb", ".xml")) {
            $src = "$PackDir\Plugins\$($dll -replace '\.dll$', '')$ext"
            if (Test-Path $src) { Copy-Item $src "$GameDir\Plugins\" -Force }
        }
    }
} else {
    Write-Host "==> BSML/SiraUtil 已存在，跳过"
}

# ---------- 4. 复制彩虹时钟 ----------
Write-Host "==> 部署 RainbowClock.dll"
Copy-Item $ModDll "$GameDir\Plugins\" -Force
if (Test-Path $ModPdb) { Copy-Item $ModPdb "$GameDir\Plugins\" -Force }

Write-Host "完成。插件目录: $GameDir\Plugins"
if ($Launch) {
    Write-Host "==> 启动游戏"
    Push-Location $GameDir
    try { & ".\Beat Saber.exe" } finally { Pop-Location }
}
