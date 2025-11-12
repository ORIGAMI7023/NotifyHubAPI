# NotifyHub API 部署更新命令

## 1. 上传应用文件

```bash
# 上传发布文件（覆盖旧文件）
scp -r D:\Programing\C#\NotifyHubAPI\NotifyHubAPI\bin\Release\net8.0\publish\* root@downf.cn:/var/www/notifyhub/
```

## 2. 上传配置文件（可选，首次部署或配置变更时）

```bash
# nginx 配置（覆盖原文件）
scp D:\Programing\C#\NotifyHubAPI\linux\notify.downf.cn.conf root@downf.cn:/etc/nginx/sites-available/notifyhub

# 上传 systemd 服务配置
scp D:\Programing\C#\NotifyHubAPI\linux\notifyhub.service root@downf.cn:/etc/systemd/system/notifyhub.service
```

## 3. 服务器端操作

### 3.1 首次部署（仅第一次需要）

```bash
# 创建软链接（启用nginx站点）
sudo ln -s /etc/nginx/sites-available/notifyhub /etc/nginx/sites-enabled/

# 创建应用目录和日志目录
sudo mkdir -p /var/www/notifyhub/logs

# 设置权限
sudo chown -R www-data:www-data /var/www/notifyhub
sudo chmod 755 /var/www/notifyhub
```

### 3.2 配置更新后（nginx或systemd配置变更时）

```bash
# 测试nginx配置
sudo nginx -t

# 重载nginx配置
sudo systemctl reload nginx

# 重载systemd配置
sudo systemctl daemon-reload

# 启用服务（开机自启）
sudo systemctl enable notifyhub
```

### 3.3 日常更新（应用文件更新后）

```bash
# 重启NotifyHub服务
sudo systemctl restart notifyhub

# 查看服务状态
sudo systemctl status notifyhub

# 查看实时日志
sudo journalctl -u notifyhub -f
```

## 4. 验证部署

```bash
# 健康检查
curl https://notify.downf.cn/health

# 查看应用日志
sudo tail -f /var/www/notifyhub/logs/notifyhub.log

# 查看nginx日志
sudo tail -f /var/log/nginx/notifyhub_access.log
sudo tail -f /var/log/nginx/notifyhub_error.log
```

## 5. 快速重启命令（常用）

```bash
# 停止、启动、重启
sudo systemctl stop notifyhub
sudo systemctl start notifyhub
sudo systemctl restart notifyhub

# 查看状态
sudo systemctl status notifyhub

# 实时日志
sudo journalctl -u notifyhub -f --lines=50
```

## 注意事项

1. **环境变量配置**：敏感信息（API密钥、SMTP密码）已配置在 `/etc/systemd/system/notifyhub.service` 中，无需上传
2. **SSL证书**：确保 `/etc/letsencrypt/live/notify.downf.cn/` 目录下有有效的SSL证书
3. **权限问题**：应用以 `www-data` 用户运行，确保目录权限正确
4. **端口占用**：应用监听 `localhost:5000`，通过nginx反向代理到443端口
