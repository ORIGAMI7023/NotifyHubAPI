# NotifyHub API 部署命令

## 1. 上传应用文件

```bash
scp -r D:\Programing\C#\NotifyHubAPI\NotifyHubAPI\bin\Release\net8.0\publish\* root@downf.cn:/var/www/notifyhub/
```

## 2. 上传配置文件（直接覆盖）

```bash
# nginx 配置（直接覆盖原文件）
scp D:\Programing\C#\NotifyHubAPI\linux\notify.downf.cn.conf root@downf.cn:/etc/nginx/conf.d/notify.downf.cn.conf

# 上传 systemd 服务配置
scp D:\Programing\C#\NotifyHubAPI\linux\notifyhub.service root@downf.cn:/etc/systemd/system/notifyhub.service
```

## 3. 配置应用（在服务器上执行）

```bash
# 重载 nginx
sudo nginx -t && sudo systemctl reload nginx

# 重启服务
sudo systemctl daemon-reload
sudo systemctl restart notifyhub
sudo systemctl status notifyhub
```

## 4. 验证部署

```bash
curl https://notify.downf.cn/health
```
