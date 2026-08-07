$ErrorActionPreference = "Stop"

#build-frontend.ps1 构建多入口 webui 到 NetCraft.TPGA\wwwroot
#webui 目录路径含 # 字符 vite 构建会失败 故复制到无特殊字符的临时目录构建
$projectDir = Split-Path -Parent $PSScriptRoot
$frontendDir = Join-Path $projectDir "webui"
$cacheDir = "C:\Users\qwq\AppData\Local\Temp\NetCraft-TPGA-WebUI-Build"
$distDir = Join-Path $cacheDir "dist"
$targetDir = Join-Path $projectDir "NetCraft.TPGA\wwwroot"

Write-Host "=== NetCraft.TPGA WebUI Frontend Build ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/5] Cleaning cache directory..." -ForegroundColor Yellow
if (Test-Path $cacheDir) {
    Remove-Item -Path $cacheDir -Recurse -Force
}
New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null

Write-Host "[2/5] Copying frontend to cache (excluding node_modules)..." -ForegroundColor Yellow
Get-ChildItem -Path $frontendDir | Where-Object { $_.Name -ne "node_modules" } | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $cacheDir -Recurse -Force
}

Write-Host "[3/5] Building frontend (admin + player multi-entry)..." -ForegroundColor Yellow
Push-Location $cacheDir
try {
    Write-Host "Current directory: $(Get-Location)" -ForegroundColor Gray
    #复用源目录 node_modules 加速增量构建 若不存在则 npm install
    $srcNodeModules = Join-Path $frontendDir "node_modules"
    if (Test-Path $srcNodeModules) {
        $cacheNodeModules = Join-Path $cacheDir "node_modules"
        if (-not (Test-Path $cacheNodeModules)) {
            Write-Host "Creating junction to source node_modules..." -ForegroundColor Gray
            #node_modules 文件多 复制慢 用 junction 指向源目录 失败则回退复制
            try {
                New-Item -ItemType Junction -Path $cacheNodeModules -Target $srcNodeModules -ErrorAction Stop | Out-Null
            } catch {
                Write-Host "Junction failed, copying node_modules..." -ForegroundColor Gray
                Copy-Item -Path $srcNodeModules -Destination $cacheNodeModules -Recurse -Force
            }
        }
    } else {
        npm install --legacy-peer-deps
        if ($LASTEXITCODE -ne 0) {
            throw "npm install failed with exit code $LASTEXITCODE"
        }
    }

    npm run build
    if ($LASTEXITCODE -ne 0) {
        throw "npm run build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host "[4/5] Copying build output to NetCraft.TPGA\wwwroot..." -ForegroundColor Yellow
if (Test-Path $targetDir) {
    Remove-Item -Path $targetDir -Recurse -Force
}
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

if (Test-Path $distDir) {
    Copy-Item -Path "$distDir\*" -Destination $targetDir -Recurse -Force
    Write-Host "Build output copied to: $targetDir" -ForegroundColor Green
}
else {
    throw "Build output directory not found: $distDir"
}

Write-Host "[5/5] Cleaning cache directory..." -ForegroundColor Yellow
Remove-Item -Path $cacheDir -Recurse -Force

Write-Host ""
Write-Host "=== Build completed successfully! ===" -ForegroundColor Green
Write-Host "Output: $targetDir" -ForegroundColor Cyan
