# Linux 生产环境配置文件

此文件夹包含 downf.cn 生产环境的配置文件备份。

## 📁 文件说明

| 文件 | 用途 | 部署路径 |
|------|------|----------|
| `downf.cn.conf` | nginx 站点配置 | `/etc/nginx/conf.d/downf.cn.conf` |
| `filehost.service` | FileHost 前端服务 | `/etc/systemd/system/filehost.service` |
| `filehostapi.service` | FileHostAPI 后端服务 | `/etc/systemd/system/filehostapi.service` |

## 🚀 部署步骤

### 1. 更新 nginx 配置

```bash
# 上传配置文件到服务器
scp linux/downf.cn.conf root@downf.cn:/tmp/

# 备份旧配置
sudo cp /etc/nginx/conf.d/downf.cn.conf /etc/nginx/conf.d/downf.cn.conf.bak

# 替换配置
sudo cp /tmp/downf.cn.conf /etc/nginx/conf.d/downf.cn.conf

# 测试配置
sudo nginx -t

# 重载 nginx
sudo systemctl reload nginx
```

### 2. 更新 systemd 服务配置

```bash
# 上传配置文件
scp linux/filehost.service root@downf.cn:/tmp/
scp linux/filehostapi.service root@downf.cn:/tmp/

# 备份旧配置
sudo cp /etc/systemd/system/filehost.service /etc/systemd/system/filehost.service.bak
sudo cp /etc/systemd/system/filehostapi.service /etc/systemd/system/filehostapi.service.bak

# 替换配置
sudo cp /tmp/filehost.service /etc/systemd/system/
sudo cp /tmp/filehostapi.service /etc/systemd/system/

# 重新加载 systemd
sudo systemctl daemon-reload

# 重启服务
sudo systemctl restart filehost filehostapi

# 检查状态
sudo systemctl status filehost filehostapi
```

## ⚠️ 重要说明

### nginx 配置要点

1. **大文件上传超时**（2025-11-06 新增）：
   - `proxy_connect_timeout 1800s`
   - `proxy_send_timeout 1800s`
   - `proxy_read_timeout 1800s`
   - 支持 30 分钟上传时间，适合慢速网络

2. **WebSocket 支持**：
   - 需要在 `/etc/nginx/nginx.conf` 的 `http` 块中添加：
     ```nginx
     map $http_upgrade $connection_upgrade {
         default upgrade;
         '' close;
     }
     ```

3. **ForwardedHeaders 支持**：
   - `X-Forwarded-For`
   - `X-Forwarded-Proto`
   - `X-Forwarded-Host`

### systemd 配置要点

1. **环境变量**（敏感信息）：
   - `Admin__Username`: 管理员用户名
   - `Admin__PasswordHash`: 密码哈希（PBKDF2-SHA256）
   - `Admin__Salt`: 密码盐值
   - `Admin__SecretKey`: HMAC 密钥
   - `ApiBaseUrl`: API 地址

2. **修改配置后必须执行**：
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl restart filehost filehostapi
   ```

## 🔄 版本历史

### 2025-11-06
- **nginx**: 添加大文件上传超时配置（30 分钟）
- **应用**: 优化文件上传机制（JavaScript 直传 API）

### 之前版本
- **nginx**: ForwardedHeaders 支持（Cookie 认证修复）
- **systemd**: 独立前后端服务配置
