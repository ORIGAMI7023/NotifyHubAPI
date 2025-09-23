# NotifyHubAPI 接口文档

## 基础信息

**服务地址**：`https://notify.origami7023.cn`  
**API版本**：v1.0  
**认证方式**：API Key

---

## 认证说明

### 支持的认证方式

1. **X-API-Key Header**
```http
X-API-Key: nh-prod-7K9mP3xR8wQ2nV5ag4yQWcL8sA9dE3tY7uI2oP6qW1zX5
```

2. **Authorization Bearer Token**
```http
Authorization: Bearer nh-prod-7K9mP3xR8wQ2nV5ag4yQWcL8sA9dE3tY7uI2oP6qW1zX5
```

### 可用API密钥
- `DEFAULT`: `nh-prod-7K9mP3xR8wQ2nV5ag4yQWcL8sA9dE3tY7uI2oP6qW1zX5`

---

## API接口列表

### 1. 发送邮件

**基础信息**
- **路径**：`POST /api/email/send`
- **功能**：发送邮件通知
- **认证**：需要API Key

**请求参数**
```json
{
  "to": ["recipient@example.com"],
  "cc": ["cc@example.com"],
  "bcc": ["bcc@example.com"],
  "subject": "邮件主题",
  "body": "邮件内容",
  "category": "NOTIFICATION",
  "isHtml": false,
  "priority": 1
}
```

**参数说明**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| to | string[] | 是 | 收件人邮箱列表 |
| cc | string[] | 否 | 抄送邮箱列表 |
| bcc | string[] | 否 | 密送邮箱列表 |
| subject | string | 是 | 邮件主题（最大500字符） |
| body | string | 是 | 邮件正文（最大50000字符） |
| category | string | 是 | 邮件分类（最大100字符） |
| isHtml | boolean | 否 | 是否HTML格式，默认false |
| priority | int | 否 | 优先级：0=低，1=正常，2=高 |

**成功响应**
```json
{
  "success": true,
  "message": "邮件发送请求已处理",
  "data": {
    "emailId": "a1b2c3d4",
    "status": 1,
    "message": "邮件发送成功"
  },
  "requestId": "req12345",
  "timestamp": "2025-09-22T14:30:00Z"
}
```

**错误响应**
```json
{
  "success": false,
  "message": "邮箱格式错误: invalid-email",
  "data": null,
  "requestId": "req12345",
  "timestamp": "2025-09-22T14:30:00Z"
}
```

**cURL示例**
```bash
curl -X POST "https://notify.origami7023.cn/api/email/send" \
  -H "Content-Type: application/json" \
  -H "X-API-Key: nh-prod-7K9mP3xR8wQ2nV5ag4yQWcL8sA9dE3tY7uI2oP6qW1zX5" \
  -d '{
    "to": ["test@example.com"],
    "subject": "测试邮件",
    "body": "这是一封测试邮件",
    "category": "TEST"
  }'
```

---

### 2. 邮件服务健康检查

**基础信息**
- **路径**：`GET /api/email/health`
- **功能**：检查邮件服务状态
- **认证**：需要API Key

**成功响应**
```json
{
  "success": true,
  "message": "服务运行正常",
  "data": {
    "status": "Healthy",
    "timestamp": "2025-09-22T14:30:00Z",
    "version": "1.0.0",
    "environment": "Production"
  },
  "requestId": "req12345",
  "timestamp": "2025-09-22T14:30:00Z"
}
```

**cURL示例**
```bash
curl -X GET "https://notify.origami7023.cn/api/email/health" \
  -H "X-API-Key: nh-prod-7K9mP3xR8wQ2nV5ag4yQWcL8sA9dE3tY7uI2oP6qW1zX5"
```

---

### 3. 系统健康检查

**基础信息**
- **路径**：`GET /health`
- **功能**：检查整个系统健康状态
- **认证**：无需认证

**响应示例**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "smtp": {
      "status": "Healthy",
      "description": "SMTP配置正常"
    },
    "apikeys": {
      "status": "Healthy", 
      "description": "发现1个API密钥"
    }
  }
}
```

**cURL示例**
```bash
curl -X GET "https://notify.origami7023.cn/health"
```

---

### 4. 系统信息

**基础信息**
- **路径**：`GET /info`
- **功能**：获取系统信息和运行状态
- **认证**：无需认证

**响应示例**
```json
{
  "service": "NotifyHubAPI",
  "version": "1.0.0 (Stateless)",
  "environment": "Production",
  "timestamp": "2025-09-22T14:30:00Z",
  "mode": "无数据库模式",
  "status": "运行正常",
  "features": {
    "emailSending": true,
    "emailHistory": false,
    "retryMechanism": false,
    "persistence": false
  },
  "message": "邮件发送功能正常，但不保存发送记录"
}
```

**cURL示例**
```bash
curl -X GET "https://notify.origami7023.cn/info"
```

---

## 状态码说明

| HTTP状态码 | 说明 |
|------------|------|
| 200 | 成功 |
| 400 | 请求参数错误 |
| 401 | 认证失败（API Key无效或缺失）|
| 429 | 请求过于频繁（触发速率限制）|
| 500 | 服务器内部错误 |

---

## 速率限制

| 规则 | 限制 |
|------|------|
| 所有端点 | 60次/分钟，1000次/小时 |
| 邮件发送 | 30次/分钟 |
| 本地IP | 200次/分钟 |

---

## 使用示例

### Python示例
```python
import requests

def send_email():
    url = "https://notify.origami7023.cn/api/email/send"
    headers = {
        "Content-Type": "application/json",
        "X-API-Key": "nh-prod-7K9mP3xR8wQ2nV5ag4yQWcL8sA9dE3tY7uI2oP6qW1zX5"
    }
    data = {
        "to": ["recipient@example.com"],
        "subject": "Python发送的邮件",
        "body": "这是通过Python脚本发送的测试邮件",
        "category": "PYTHON_TEST"
    }
    
    response = requests.post(url, json=data, headers=headers)
    return response.json()
```

### JavaScript示例
```javascript
async function sendEmail() {
    const response = await fetch('https://notify.origami7023.cn/api/email/send', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-API-Key': 'nh-prod-7K9mP3xR8wQ2nV5ag4yQWcL8sA9dE3tY7uI2oP6qW1zX5'
        },
        body: JSON.stringify({
            to: ['recipient@example.com'],
            subject: 'JavaScript发送的邮件',
            body: '这是通过JavaScript发送的测试邮件',
            category: 'JS_TEST'
        })
    });
    
    return await response.json();
}
```

### PHP示例
```php
<?php
$url = 'https://notify.origami7023.cn/api/email/send';
$data = [
    'to' => ['recipient@example.com'],
    'subject' => 'PHP发送的邮件',
    'body' => '这是通过PHP发送的测试邮件',
    'category' => 'PHP_TEST'
];

$options = [
    'http' => [
        'header' => "Content-type: application/json\r\n" .
                   "X-API-Key: nh-prod-7K9mP3xR8wQ2nV5ag4yQWcL8sA9dE3tY7uI2oP6qW1zX5\r\n",
        'method' => 'POST',
        'content' => json_encode($data)
    ]
];

$context = stream_context_create($options);
$result = file_get_contents($url, false, $context);
echo $result;
?>
```

这个API文档涵盖了当前项目的所有可用接口，你可以根据实际需要调整密钥和配置信息。