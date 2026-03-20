# NotifyHub API 部署命令

## 推荐方式：使用自动部署脚本

```powershell
cd D:\Programing\C#\NotifyHubAPI
.\linux\deploy.ps1
```

---

## 手动部署步骤

### 1. 上传应用文件

```bash
scp -r D:\Programing\C#\NotifyHubAPI\NotifyHubAPI\bin\Release\net8.0\publish\* origami@downf.cn:/var/www/notifyhub/
```

### 2. 上传配置文件（通过 /tmp/ 中转）

```bash
# nginx 配置（上传到 /tmp/ 然后移动）
scp D:\Programing\C#\NotifyHubAPI\linux\notify.downf.cn.conf origami@downf.cn:/tmp/notify.downf.cn.conf
ssh origami@downf.cn "sudo mv /tmp/notify.downf.cn.conf /etc/nginx/conf.d/notify.downf.cn.conf"

# 上传 systemd 服务配置
scp D:\Programing\C#\NotifyHubAPI\linux\notifyhub.service origami@downf.cn:/tmp/notifyhub.service
ssh origami@downf.cn "sudo mv /tmp/notifyhub.service /etc/systemd/system/notifyhub.service"
```

### 3. 配置应用（在服务器上执行）

```bash
# 设置文件所有权
sudo chown -R origami:origami /var/www/notifyhub

# 重载 nginx
sudo nginx -t && sudo systemctl reload nginx

# 重启服务
sudo systemctl daemon-reload
sudo systemctl restart notifyhub
sudo systemctl status notifyhub
```

### 4. 验证部署

```bash
curl https://notify.downf.cn/health
```
