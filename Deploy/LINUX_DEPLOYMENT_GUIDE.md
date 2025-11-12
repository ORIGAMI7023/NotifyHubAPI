# NotifyHub API - Linux 部署指南

## 📋 部署概述

本指南将帮助您将NotifyHub API从Windows IIS迁移到Linux systemd环境。

**目标环境**：
- 操作系统：Linux (Ubuntu/Debian)
- Web服务器：Nginx (反向代理)
- 进程管理：systemd
- 域名：notify.downf.cn

---

## ✅ 前置准备检查清单

### 服务器要求

- [ ] Linux服务器（推荐Ubuntu 20.04/22.04或Debian 11/12）
- [ ] .NET 8.0 Runtime已安装
- [ ] Nginx已安装
- [ ] SSL证书已准备（notify.downf.cn）
- [ ] 具有sudo权限的用户账户

### 配置准备

- [ ] SMTP服务器信息（主机、端口、用户名、密码）
- [ ] API密钥（至少1个）
- [ ] 数据库文件（如果使用SQLite）

---

## 📦 步骤1：安装依赖

### 1.1 安装.NET 8.0 Runtime

```bash
# 下载并执行安装脚本
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh

# 安装ASP.NET Core Runtime 8.0
./dotnet-install.sh --runtime aspnetcore --version 8.0

# 添加到PATH（添加到~/.bashrc或~/.profile）
export PATH="$PATH:$HOME/.dotnet"
export DOTNET_ROOT=$HOME/.dotnet

# 验证安装
dotnet --info
```

### 1.2 安装Nginx

```bash
# Ubuntu/Debian
sudo apt update
sudo apt install nginx -y

# 验证安装
nginx -v

# 启动Nginx
sudo systemctl start nginx
sudo systemctl enable nginx
```

---

## 🚀 步骤2：部署应用程序

### 2.1 发布应用

**在开发机器上**：

```bash
cd /path/to/NotifyHubAPI
dotnet publish -c Release -o ./publish
```

### 2.2 上传到服务器

```bash
# 压缩发布文件
tar -czf notifyhub.tar.gz -C ./publish .

# 上传到服务器（使用scp）
scp notifyhub.tar.gz user@your-server:/tmp/

# 或者使用rsync
rsync -avz --progress ./publish/ user@your-server:/tmp/notifyhub/
```

### 2.3 部署到目标目录

**在服务器上**：

```bash
# 创建应用目录
sudo mkdir -p /var/www/notifyhub
sudo mkdir -p /var/www/notifyhub/logs

# 解压应用文件
cd /tmp
tar -xzf notifyhub.tar.gz -C /var/www/notifyhub/

# 设置权限
sudo chown -R www-data:www-data /var/www/notifyhub
sudo chmod 755 /var/www/notifyhub
sudo chmod 755 /var/www/notifyhub/logs

# 如果有SQLite数据库
sudo chmod 644 /var/www/notifyhub/notifyhub.db
sudo chown www-data:www-data /var/www/notifyhub/notifyhub.db
```

---

## ⚙️ 步骤3：配置systemd服务

### 3.1 复制服务配置文件

```bash
# 复制配置文件到systemd目录
sudo cp /path/to/Deploy/notifyhub.service /etc/systemd/system/

# 或者直接创建
sudo nano /etc/systemd/system/notifyhub.service
```

### 3.2 修改环境变量

**编辑服务文件**：

```bash
sudo nano /etc/systemd/system/notifyhub.service
```

**必须修改以下环境变量**：

```ini
# 替换为实际的API密钥
Environment="NOTIFYHUB_APIKEY_DEFAULT=your-actual-api-key-here"

# 替换为实际的SMTP密码
Environment="NOTIFYHUB_SMTP_PASSWORD=your-actual-smtp-password"

# 如果SMTP用户名或邮箱变更，也需要修改
Environment="NOTIFYHUB_SMTP_USERNAME=your-email@example.com"
Environment="NOTIFYHUB_SMTP_FROMEMAIL=your-email@example.com"
```

### 3.3 启动服务

```bash
# 重载systemd配置
sudo systemctl daemon-reload

# 启用开机自启
sudo systemctl enable notifyhub

# 启动服务
sudo systemctl start notifyhub

# 查看服务状态
sudo systemctl status notifyhub

# 查看实时日志
sudo journalctl -u notifyhub -f
```

### 3.4 验证应用运行

```bash
# 测试本地端口（应该返回健康状态）
curl http://localhost:5000/health

# 预期输出：
# {"status":"Healthy","results":{...}}
```

---

## 🌐 步骤4：配置Nginx反向代理

### 4.1 复制Nginx配置

```bash
# 复制配置文件
sudo cp /path/to/Deploy/nginx-notifyhub.conf /etc/nginx/sites-available/notifyhub

# 或者直接创建
sudo nano /etc/nginx/sites-available/notifyhub
```

### 4.2 修改SSL证书路径

**编辑Nginx配置**：

```bash
sudo nano /etc/nginx/sites-available/notifyhub
```

**修改SSL证书路径**（第28-30行）：

```nginx
ssl_certificate /path/to/your/fullchain.pem;
ssl_certificate_key /path/to/your/privkey.pem;
ssl_trusted_certificate /path/to/your/chain.pem;
```

### 4.3 启用站点配置

```bash
# 创建软链接
sudo ln -s /etc/nginx/sites-available/notifyhub /etc/nginx/sites-enabled/

# 测试配置
sudo nginx -t

# 如果测试通过，重载Nginx
sudo systemctl reload nginx
```

### 4.4 配置防火墙

```bash
# 允许HTTP和HTTPS
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# 查看防火墙状态
sudo ufw status
```

---

## 🧪 步骤5：测试部署

### 5.1 健康检查

```bash
# 测试HTTPS健康检查
curl https://notify.downf.cn/health

# 预期输出：
# {"status":"Healthy",...}
```

### 5.2 测试邮件发送

```bash
curl -X POST https://notify.downf.cn/api/email/send \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key-here" \
  -d '{
    "to": ["test@example.com"],
    "subject": "测试邮件",
    "body": "这是一封测试邮件",
    "category": "TEST",
    "isHtml": false
  }'

# 预期输出：
# {"success":true,"message":"邮件发送请求已处理",...}
```

### 5.3 测试限流

```bash
# 使用ab工具测试（应该被限流）
ab -n 100 -c 10 https://notify.downf.cn/health

# 预期：部分请求返回429 Too Many Requests
```

---

## 📊 步骤6：监控和日志

### 6.1 查看应用日志

```bash
# 查看systemd日志
sudo journalctl -u notifyhub -f

# 查看应用日志文件
sudo tail -f /var/www/notifyhub/logs/notifyhub.log

# 查看安全扫描日志
sudo tail -f /var/www/notifyhub/logs/security-scan.log

# 查看邮件发送日志
sudo tail -f /var/www/notifyhub/logs/email-sent-*.log
```

### 6.2 查看Nginx日志

```bash
# 访问日志
sudo tail -f /var/log/nginx/notifyhub_access.log

# 错误日志
sudo tail -f /var/log/nginx/notifyhub_error.log
```

### 6.3 配置日志轮转（可选）

```bash
# 创建logrotate配置
sudo nano /etc/logrotate.d/notifyhub
```

**内容**：

```
/var/www/notifyhub/logs/*.log {
    daily
    rotate 30
    compress
    delaycompress
    notifempty
    missingok
    copytruncate
}
```

---

## 🔧 常见问题排查

### 问题1：服务启动失败

```bash
# 查看详细错误信息
sudo journalctl -u notifyhub -n 50 --no-pager

# 常见原因：
# 1. 端口5000被占用
sudo lsof -i :5000

# 2. 权限问题
sudo chown -R www-data:www-data /var/www/notifyhub

# 3. .NET运行时未找到
which dotnet
```

### 问题2：Nginx 502 Bad Gateway

```bash
# 检查后端服务是否运行
curl http://localhost:5000/health

# 检查Nginx错误日志
sudo tail -f /var/log/nginx/error.log

# 检查SELinux（如果启用）
sudo setsebool -P httpd_can_network_connect 1
```

### 问题3：健康检查失败

```bash
# 检查环境变量是否正确配置
sudo systemctl show notifyhub --property=Environment

# 检查API密钥配置
# 确保至少有一个NOTIFYHUB_APIKEY_*环境变量
```

### 问题4：邮件发送失败

```bash
# 检查SMTP配置
sudo journalctl -u notifyhub | grep SMTP

# 测试SMTP连接
telnet smtp.qq.com 587

# 检查防火墙是否阻止出站连接
sudo iptables -L OUTPUT -n -v
```

---

## 🔄 更新应用

### 更新步骤

```bash
# 1. 停止服务
sudo systemctl stop notifyhub

# 2. 备份当前版本
sudo cp -r /var/www/notifyhub /var/www/notifyhub.backup.$(date +%Y%m%d)

# 3. 上传并解压新版本
sudo tar -xzf notifyhub-new.tar.gz -C /var/www/notifyhub/

# 4. 恢复权限
sudo chown -R www-data:www-data /var/www/notifyhub

# 5. 启动服务
sudo systemctl start notifyhub

# 6. 验证
sudo systemctl status notifyhub
curl https://notify.downf.cn/health
```

---

## 📝 维护命令速查

```bash
# 服务管理
sudo systemctl start notifyhub      # 启动
sudo systemctl stop notifyhub       # 停止
sudo systemctl restart notifyhub    # 重启
sudo systemctl status notifyhub     # 状态
sudo systemctl enable notifyhub     # 开机自启
sudo systemctl disable notifyhub    # 禁用自启

# 日志查看
sudo journalctl -u notifyhub -f                    # 实时日志
sudo journalctl -u notifyhub --since "1 hour ago"  # 最近1小时
sudo journalctl -u notifyhub --since today         # 今天的日志

# Nginx管理
sudo nginx -t                      # 测试配置
sudo systemctl reload nginx        # 重载配置
sudo systemctl restart nginx       # 重启Nginx

# 清理日志
sudo journalctl --vacuum-time=7d   # 清理7天前的systemd日志
```

---

## 🎯 部署检查清单

完成部署后，请确认以下各项：

- [ ] .NET 8.0 Runtime已安装
- [ ] 应用文件已部署到 /var/www/notifyhub
- [ ] 目录权限正确（www-data:www-data）
- [ ] systemd服务配置正确
- [ ] 环境变量已配置（API密钥、SMTP）
- [ ] systemd服务已启动并启用自启
- [ ] Nginx配置已部署
- [ ] SSL证书路径正确
- [ ] Nginx配置测试通过
- [ ] 防火墙规则已配置
- [ ] 健康检查返回正常
- [ ] 邮件发送功能正常
- [ ] 日志输出正常
- [ ] 限流功能工作正常

---

## 📞 技术支持

如果遇到问题：

1. 检查应用日志：`sudo journalctl -u notifyhub -n 100`
2. 检查Nginx日志：`sudo tail -100 /var/log/nginx/notifyhub_error.log`
3. 检查配置文件：确保所有环境变量正确
4. 验证网络连接：确保SMTP端口可访问

---

**部署完成！** 🎉

您的NotifyHub API现在应该已经在Linux服务器上正常运行了。
