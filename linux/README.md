# Linux 部署配置文件

本目录包含NotifyHub API在Linux服务器上部署所需的配置文件和部署命令。

## 📁 文件说明

### 配置文件

| 文件名 | 用途 | 目标位置 |
|--------|------|----------|
| `notifyhub.service` | systemd服务配置 | `/etc/systemd/system/notifyhub.service` |
| `notify.downf.cn.conf` | Nginx反向代理配置 | `/etc/nginx/sites-available/notifyhub` |

### 文档文件

| 文件名 | 说明 |
|--------|------|
| `upload.md` | 完整部署文档（首次部署和更新） |
| `quick-update.md` | 快速更新命令（日常使用） |
| `README.md` | 本文件 |

---

## 🚀 快速开始

### 首次部署

参考 `upload.md` 中的完整步骤，包括：
1. 上传应用文件
2. 上传配置文件
3. 配置服务器环境
4. 启动服务

### 日常更新

参考 `quick-update.md`，使用以下命令快速更新：

```bash
# 上传应用
scp -r D:\Programing\C#\NotifyHubAPI\NotifyHubAPI\bin\Release\net8.0\publish\* root@downf.cn:/var/www/notifyhub/

# 重启服务
ssh root@downf.cn "sudo systemctl restart notifyhub"
```

---

## ⚙️ 配置说明

### systemd 服务配置

**文件**：`notifyhub.service`

**关键配置**：
- 运行用户：`www-data`
- 工作目录：`/var/www/notifyhub`
- 监听地址：`http://localhost:5000`
- 环境变量：包含API密钥和SMTP配置

**⚠️ 注意**：首次部署时需要修改环境变量中的敏感信息：
- `NOTIFYHUB_APIKEY_DEFAULT`：替换为实际API密钥
- `NOTIFYHUB_SMTP_PASSWORD`：替换为实际SMTP密码

### Nginx 配置

**文件**：`notify.downf.cn.conf`

**关键配置**：
- HTTP自动跳转HTTPS
- SSL/TLS 1.2/1.3
- 反向代理到 `localhost:5000`
- 安全响应头（HSTS、CSP等）
- 请求体大小限制：1MB

**⚠️ 注意**：需要修改SSL证书路径（第28-30行）

---

## 📋 部署信息

| 配置项 | 值 |
|--------|-----|
| **应用名称** | NotifyHub API |
| **域名** | notify.downf.cn |
| **应用目录** | /var/www/notifyhub |
| **监听端口** | localhost:5000 |
| **服务名** | notifyhub |
| **运行用户** | www-data |
| **发布目录** | D:\Programing\C#\NotifyHubAPI\NotifyHubAPI\bin\Release\net8.0\publish |

---

## 🔧 常用命令

### 服务管理

```bash
sudo systemctl start notifyhub      # 启动
sudo systemctl stop notifyhub       # 停止
sudo systemctl restart notifyhub    # 重启
sudo systemctl status notifyhub     # 状态
sudo systemctl enable notifyhub     # 开机自启
```

### 日志查看

```bash
# systemd日志
sudo journalctl -u notifyhub -f

# 应用日志
sudo tail -f /var/www/notifyhub/logs/notifyhub.log

# Nginx日志
sudo tail -f /var/log/nginx/notifyhub_access.log
```

### 配置重载

```bash
# 重载systemd配置
sudo systemctl daemon-reload

# 重载nginx配置
sudo nginx -t && sudo systemctl reload nginx
```

---

## ✅ 验证部署

```bash
# 健康检查
curl https://notify.downf.cn/health

# 应返回类似：
# {"status":"Healthy","results":{...}}
```

---

## 📚 相关文档

- [完整部署指南](../Deploy/LINUX_DEPLOYMENT_GUIDE.md)
- [修改日志](../Deploy/CHANGELOG.md)
- [上传命令](upload.md)
- [快速更新](quick-update.md)

---

**最后更新**：2025-11-12
