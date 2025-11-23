# NotifyHub API 修改日志

## 🔄 Linux迁移准备 - 2025-11-12

### 📝 代码修改

#### 1. **域名配置更新**

**文件：`NotifyHubAPI/appsettings.json`**
- ✅ 更新 `Security:AllowedHosts` 域名
  - `notify.origami7023.cn` → `notify.downf.cn`
  - `www.notify.origami7023.cn` → `www.notify.downf.cn`

**文件：`NotifyHubAPI/Program.cs` (第215行)**
- ✅ 更新CORS默认域名
  - `https://notify.origami7023.cn` → `https://notify.downf.cn`

**文件：`NotifyHubAPI/Middleware/HostFilteringMiddleware.cs` (第26行)**
- ✅ 更新默认允许主机
  - `notify.origami7023.cn` → `notify.downf.cn`

---

#### 2. **敏感信息清理**

**文件：`NotifyHubAPI/appsettings.json`**
- ✅ 清空SMTP配置中的敏感字段
  - `Host`: 置空（由环境变量提供）
  - `Username`: 置空
  - `Password`: 置空
  - `FromEmail`: 置空
- ✅ 保留非敏感配置
  - `Port`: 587
  - `UseSsl`: true
  - `FromName`: "NotifyHub"

---

#### 3. **数据库连接字符串修正**

**文件：`NotifyHubAPI/appsettings.json`**
- ✅ 更新ConnectionStrings
  - 旧值：`Server=localhost;Database=NotificationApi;Trusted_Connection=true;TrustServerCertificate=true;`
  - 新值：`Data Source=notifyhub.db` (SQLite格式)

---

#### 4. **健康检查优化 - 不暴露密钥数量**

**文件：`NotifyHubAPI/Program.cs` (第199-207行)**
- ✅ 生产环境不显示API密钥数量
  - 开发环境：`共有{count}个API密钥`
  - 生产环境：`API密钥配置正常`
- ✅ 降低信息泄露风险

**代码差异**：
```csharp
// 修改前
return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy($"共有{apiKeyCount}个API密钥");

// 修改后
if (apiKeyCount > 0)
{
    var message = builder.Environment.IsDevelopment()
        ? $"共有{apiKeyCount}个API密钥"
        : "API密钥配置正常";
    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(message);
}
```

---

#### 5. **邮件日志脱敏处理**

**文件：`NotifyHubAPI/Services/EmailService.cs`**
- ✅ 新增邮箱脱敏方法 `MaskEmail(string email)`
  - 示例：`user@example.com` → `u***@example.com`
- ✅ 新增批量脱敏方法 `MaskEmailList(List<string>? emails)`
- ✅ 更新日志记录，使用脱敏后的邮箱地址
  - To字段：脱敏处理
  - Cc字段：脱敏处理
  - From字段：脱敏处理
  - Bcc字段：仅显示数量

**代码差异**：
```csharp
// 修改前
$"To: {(emailRequest.To != null ? string.Join(", ", emailRequest.To) : "")} | "
$"From: {_smtpSettings.FromEmail} | "

// 修改后
$"To: {MaskEmailList(emailRequest.To)} | "
$"From: {MaskEmail(_smtpSettings.FromEmail ?? "")} | "
```

---

### 📦 新增配置文件

#### 1. **systemd服务配置**

**文件：`Deploy/notifyhub.service`**
- ✅ 完整的systemd单元文件
- ✅ 包含环境变量配置模板
- ✅ 安全加固配置
  - NoNewPrivileges
  - PrivateTmp
  - ProtectSystem=strict
  - ProtectHome
- ✅ 自动重启配置
- ✅ 资源限制示例

**环境变量清单**：
- `ASPNETCORE_ENVIRONMENT`
- `ASPNETCORE_URLS`
- `NOTIFYHUB_APIKEY_DEFAULT`
- `NOTIFYHUB_SMTP_HOST`
- `NOTIFYHUB_SMTP_PORT`
- `NOTIFYHUB_SMTP_USESSL`
- `NOTIFYHUB_SMTP_USERNAME`
- `NOTIFYHUB_SMTP_PASSWORD`
- `NOTIFYHUB_SMTP_FROMEMAIL`
- `NOTIFYHUB_SMTP_FROMNAME`

---

#### 2. **Nginx反向代理配置**

**文件：`Deploy/nginx-notifyhub.conf`**
- ✅ HTTP自动重定向到HTTPS
- ✅ SSL/TLS配置
  - TLS 1.2/1.3
  - 现代加密套件
  - OCSP Stapling
- ✅ 安全响应头
  - HSTS (1年有效期)
  - X-Frame-Options: DENY
  - X-Content-Type-Options: nosniff
  - Content-Security-Policy
- ✅ 反向代理配置
  - 传递真实客户端IP
  - 请求体大小限制1MB
  - 超时配置
- ✅ 日志配置

---

#### 3. **部署指南文档**

**文件：`Deploy/LINUX_DEPLOYMENT_GUIDE.md`**
- ✅ 完整的Linux部署步骤
- ✅ 前置准备检查清单
- ✅ 依赖安装说明
  - .NET 8.0 Runtime
  - Nginx
- ✅ 应用部署步骤
- ✅ systemd配置说明
- ✅ Nginx配置说明
- ✅ 测试验证步骤
- ✅ 日志监控指南
- ✅ 常见问题排查
- ✅ 更新和维护命令

---

### 🔒 安全改进

| 改进项 | 说明 | 影响 |
|--------|------|------|
| **健康检查脱敏** | 生产环境不显示API密钥数量 | 降低信息泄露风险 |
| **邮件日志脱敏** | 邮箱地址脱敏 (u***@example.com) | 保护用户隐私 |
| **敏感信息清理** | appsettings.json中移除明文密码 | 防止配置文件泄露 |
| **环境变量配置** | 所有敏感信息通过环境变量传递 | 符合12-Factor最佳实践 |

---

### 🚀 部署改进

| 改进项 | 说明 |
|--------|------|
| **systemd安全加固** | NoNewPrivileges, ProtectSystem, ProtectHome |
| **自动重启机制** | 失败后自动重启，限制重启频率 |
| **日志集中管理** | systemd journal + 应用日志文件 |
| **资源限制** | 可选的内存和CPU限制 |
| **Nginx安全头** | HSTS, CSP, X-Frame-Options等 |

---

### 📋 迁移检查清单

#### 配置修改
- [x] 域名配置已更新（3个文件）
- [x] 敏感信息已清理
- [x] 数据库连接字符串已修正
- [x] 健康检查已优化
- [x] 日志脱敏已实现

#### 部署文件
- [x] systemd服务配置已创建
- [x] Nginx配置已创建
- [x] 部署指南已编写

#### 待部署操作
- [ ] 更新服务器环境变量（参考notifyhub.service）
- [ ] 修改Nginx配置中的SSL证书路径
- [ ] 部署应用到 /var/www/notifyhub
- [ ] 启动systemd服务
- [ ] 配置Nginx反向代理
- [ ] 验证健康检查
- [ ] 测试邮件发送功能

---

### 🎯 与Windows IIS的差异

| 配置项 | Windows IIS | Linux systemd |
|--------|-------------|---------------|
| **环境变量** | web.config | systemd service文件 |
| **监听地址** | IIS自动管理 | 必须指定 `ASPNETCORE_URLS` |
| **进程管理** | IIS | systemd |
| **日志** | IIS日志 + 应用日志 | systemd journal + 应用日志 |
| **反向代理** | IIS | Nginx |
| **SSL终止** | IIS | Nginx |
| **自动重启** | IIS自动 | systemd Restart=always |

---

### 📊 修改统计

- **修改文件**：4个
  - appsettings.json
  - Program.cs
  - HostFilteringMiddleware.cs
  - EmailService.cs
- **新增文件**：4个
  - Deploy/notifyhub.service
  - Deploy/nginx-notifyhub.conf
  - Deploy/LINUX_DEPLOYMENT_GUIDE.md
  - Deploy/CHANGELOG.md
- **新增方法**：2个
  - MaskEmail()
  - MaskEmailList()

---

### ⚡ 下一步行动

1. **提交代码修改**
   ```bash
   git add .
   git commit -m "准备Linux迁移：更新域名配置、清理敏感信息、添加部署配置"
   git push
   ```

2. **准备服务器**
   - 安装.NET 8.0 Runtime
   - 安装Nginx
   - 确认SSL证书

3. **执行部署**
   - 参考 `Deploy/LINUX_DEPLOYMENT_GUIDE.md`
   - 配置环境变量（参考web.config）
   - 部署应用文件
   - 配置并启动服务

4. **验证功能**
   - 健康检查
   - 邮件发送
   - 限流功能
   - 日志输出

---

**修改完成时间**：2025-11-12
**目标部署环境**：Linux + systemd + Nginx
**目标域名**：notify.downf.cn
