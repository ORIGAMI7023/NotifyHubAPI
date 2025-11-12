# 快速更新命令（日常使用）

## 完整更新流程（一键复制）

```bash
# 1. 上传应用文件
scp -r D:\Programing\C#\NotifyHubAPI\NotifyHubAPI\bin\Release\net8.0\publish\* root@downf.cn:/var/www/notifyhub/

# 2. 重启服务（在服务器上执行）
ssh root@downf.cn "sudo systemctl restart notifyhub && sudo systemctl status notifyhub"
```

## 仅更新应用（不重启）

```bash
scp -r D:\Programing\C#\NotifyHubAPI\NotifyHubAPI\bin\Release\net8.0\publish\* root@downf.cn:/var/www/notifyhub/
```

## 仅重启服务（服务器端）

```bash
sudo systemctl restart notifyhub && sudo systemctl status notifyhub
```

## 查看实时日志（服务器端）

```bash
# 查看systemd日志
sudo journalctl -u notifyhub -f --lines=50

# 查看应用日志
sudo tail -f /var/www/notifyhub/logs/notifyhub.log

# 查看邮件发送日志
sudo tail -f /var/www/notifyhub/logs/email-sent-*.log
```

## 健康检查

```bash
curl https://notify.downf.cn/health
```

## 测试邮件发送

```bash
curl -X POST https://notify.downf.cn/api/email/send \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "to": ["test@example.com"],
    "subject": "测试邮件",
    "body": "这是一封测试邮件",
    "category": "TEST",
    "isHtml": false
  }'
```
