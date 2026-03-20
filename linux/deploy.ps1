# ============================================================
# NotifyHub API Deploy Script
# 完整部署（构建 + 配置 + 代码 + 重启）
# ============================================================

$ErrorActionPreference = "Stop"

# 服务器配置
$SERVER = "origami@downf.cn"
$PROJECT_ROOT = "D:\Programing\C#\NotifyHubAPI"

function Write-Step {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Yellow
    Write-Host "=> $Message" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

try {
    # Step 1: Build API
    Write-Step "Step 1/6: Building NotifyHub API"
    Set-Location $PROJECT_ROOT
    dotnet publish NotifyHubAPI/NotifyHubAPI.csproj -c Release -o NotifyHubAPI/bin/Release/net8.0/publish --runtime linux-x64 --self-contained false
    if ($LASTEXITCODE -ne 0) { throw "API build failed" }
    Write-Success "API build completed"

    # Step 2: Upload nginx config to temp and install
    Write-Step "Step 2/6: Installing nginx config"
    scp linux/notify.downf.cn.conf ${SERVER}:/tmp/notify.downf.cn.conf
    if ($LASTEXITCODE -ne 0) { throw "nginx config upload failed" }
    ssh $SERVER "sudo mv /tmp/notify.downf.cn.conf /etc/nginx/conf.d/notify.downf.cn.conf"
    if ($LASTEXITCODE -ne 0) { throw "nginx config install failed" }
    Write-Success "nginx config installed"

    # Step 3: Upload systemd service config to temp and install
    Write-Step "Step 3/6: Installing systemd service config"
    scp linux/notifyhub.service ${SERVER}:/tmp/notifyhub.service
    if ($LASTEXITCODE -ne 0) { throw "notifyhub.service upload failed" }
    ssh $SERVER "sudo mv /tmp/notifyhub.service /etc/systemd/system/notifyhub.service"
    if ($LASTEXITCODE -ne 0) { throw "notifyhub.service install failed" }
    Write-Success "notifyhub.service installed"

    # Step 4: Upload API
    Write-Step "Step 4/6: Uploading API"
    ssh $SERVER "mkdir -p /var/www/notifyhub/logs"
    scp -r NotifyHubAPI/bin/Release/net8.0/publish/* ${SERVER}:/var/www/notifyhub/
    if ($LASTEXITCODE -ne 0) { throw "API upload failed" }
    Write-Success "API upload completed"

    # Step 5: Set directory ownership
    Write-Step "Step 5/6: Setting directory ownership"
    ssh $SERVER "sudo chown -R origami:origami /var/www/notifyhub"
    if ($LASTEXITCODE -ne 0) { throw "Directory ownership setup failed" }
    Write-Success "Directory ownership set to origami:origami"

    # Step 6: Test and reload nginx
    Write-Step "Step 6/6: Testing and reloading nginx and restarting service"
    $reloadCommand = 'sudo nginx -t && sudo systemctl reload nginx && sudo systemctl daemon-reload && sudo systemctl enable notifyhub && sudo systemctl restart notifyhub && sleep 2 && sudo systemctl status notifyhub'
    ssh $SERVER $reloadCommand
    if ($LASTEXITCODE -ne 0) { throw "Service reload failed" }
    Write-Success "Nginx and service reloaded"

    # Done
    Write-Step "Full Deployment Completed!"
    Write-Success "API URL: https://notify.downf.cn"
    Write-Success "Health Check: https://notify.downf.cn/health"
    Write-Host "`nView logs:" -ForegroundColor Cyan
    Write-Host "  ssh $SERVER 'journalctl -u notifyhub -f'" -ForegroundColor Gray
    Write-Host "`nTest API:" -ForegroundColor Cyan
    Write-Host "  curl https://notify.downf.cn/health" -ForegroundColor Gray

} catch {
    Write-ErrorMsg "Deployment failed: $_"
    Write-Host "`nDeployment aborted" -ForegroundColor Red
    exit 1
}
