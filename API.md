# NotifyHub API 文档

## 概述

NotifyHub 是一个邮件通知服务 API，提供简单、安全的邮件发送功能。

**基础信息**
- 服务地址：`https://notify.downf.cn`
- API 版本：`v1.0`
- 协议：HTTPS
- 数据格式：JSON

---

## 认证

所有 API 请求需要提供 API 密钥进行认证。支持两种认证方式：

### 方式 1：Authorization Header（推荐）
```http
Authorization: Bearer nh-prod-Y63jT32xDaxWy1Ek******
```

### 方式 2：X-API-Key Header
```http
X-API-Key: nh-prod-Y63jT32xDaxWy1Ek******
```

⚠️ **安全提示**：不支持在 URL 查询参数中传递 API 密钥。

---

## 接口列表

### 1. 发送邮件

**接口地址**
```
POST /api/email/send
```

**请求头**
```http
Content-Type: application/json
Authorization: Bearer nh-prod-Y63jT32xDaxWy1Ek******
```

**请求参数**

| 参数名 | 类型 | 必填 | 说明 | 限制 |
|--------|------|------|------|------|
| to | array | 是 | 收件人邮箱列表 | 至少1个 |
| cc | array | 否 | 抄送邮箱列表 | - |
| bcc | array | 否 | 密送邮箱列表 | - |
| subject | string | 是 | 邮件主题 | 最大500字符 |
| body | string | 是 | 邮件正文 | 最大50000字符 |
| isHtml | boolean | 否 | 是否HTML格式 | 默认false |
| priority | integer | 否 | 优先级 | 0=低, 1=普通(默认), 2=高 |
| category | string | 是 | 邮件分类 | 最大100字符，用于日志分类 |
| attachments | object | 否 | 附件字典 | key=文件名, value=Base64内容 |

**限制说明**
- 收件人总数（to + cc + bcc）不超过 100 个
- 频率限制：1分钟5次，1小时50次

**附件限制**
- 单个附件最大 5MB
- 最多 5 个附件
- 附件总大小最大 15MB
- 必须使用 Base64 编码
- 不允许的文件类型: .exe, .bat, .cmd, .com, .pif, .scr, .vbs, .js, .jar
- 文件名支持中文，最长255字符

**请求示例**

```bash
curl -X POST https://notify.downf.cn/api/email/send \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer nh-prod-Y63jT32xDaxWy1Ek******" \
  -d '{
    "to": ["user@example.com"],
    "subject": "测试邮件",
    "body": "这是一封测试邮件",
    "category": "test",
    "isHtml": false,
    "priority": 1
  }'
```

**带附件的请求示例**

```bash
curl -X POST https://notify.downf.cn/api/email/send \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer nh-prod-Y63jT32xDaxWy1Ek******" \
  -d '{
    "to": ["user@example.com"],
    "subject": "月度报告",
    "body": "<h1>请查看附件中的月度报告</h1>",
    "category": "report",
    "isHtml": true,
    "attachments": {
      "报告.pdf": "JVBERi0xLjcKCjEgMCBvYmo...",
      "数据.xlsx": "UEsDBBQABgAIAAAAIQ...",
      "图片.png": "iVBORw0KGgoAAAANSUhEUgAA..."
    }
  }'
```

**Base64 编码示例（Python）**

```python
import base64

def file_to_base64(file_path):
    with open(file_path, 'rb') as f:
        return base64.b64encode(f.read()).decode('utf-8')

# 使用
attachments = {
    "document.pdf": file_to_base64("document.pdf"),
    "image.png": file_to_base64("image.png")
}
```

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

**失败响应**
```json
{
  "success": false,
  "message": "邮箱格式错误: invalid-email",
  "data": null,
  "requestId": "req12345",
  "timestamp": "2025-09-22T14:30:00Z"
}
```

---

### 2. 健康检查

**接口地址**
```
GET /api/email/health
```

**请求头**
```http
Authorization: Bearer nh-prod-Y63jT32xDaxWy1Ek******
```

**请求示例**
```bash
curl -X GET https://notify.downf.cn/api/email/health \
  -H "Authorization: Bearer nh-prod-Y63jT32xDaxWy1Ek******"
```

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

---

### 3. 系统健康检查

**接口地址**
```
GET /health
```

**说明**：检查整个系统健康状态，无需认证

**请求示例**
```bash
curl -X GET https://notify.downf.cn/health
```

**响应示例**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "checks": [
    {
      "name": "smtp",
      "status": "Healthy",
      "description": "SMTP配置正常"
    },
    {
      "name": "apikeys",
      "status": "Healthy",
      "description": "发现1个API密钥"
    }
  ]
}
```

---

### 4. 系统信息

**接口地址**
```
GET /info
```

**说明**：获取系统信息和运行状态，无需认证

**请求示例**
```bash
curl -X GET https://notify.downf.cn/info
```

**响应示例**
```json
{
  "service": "NotifyHubAPI",
  "version": "1.0.0 (Secure)",
  "environment": "Production",
  "timestamp": "2025-09-22T14:30:00Z",
  "security": {
    "httpsOnly": true,
    "hostFiltering": true,
    "rateLimiting": true,
    "securityHeaders": true,
    "requestValidation": true
  },
  "features": {
    "emailSending": true,
    "emailHistory": false,
    "retryMechanism": false,
    "persistence": false
  }
}
```

---

## HTTP 状态码

| 状态码 | 说明 |
|--------|------|
| 200 | 请求成功 |
| 400 | 请求参数错误 |
| 401 | 未授权（API密钥无效或缺失） |
| 429 | 请求频率超限 |
| 500 | 服务器内部错误 |

---

## 速率限制

| 规则 | 限制 |
|------|------|
| 所有端点 | 60次/分钟，1000次/小时 |
| 邮件发送 | 30次/分钟 |
| 本地IP | 200次/分钟 |

---

## 错误码

| 错误码 | 说明 |
|--------|------|
| ValidationError | 验证错误 |
| InvalidParameter | 参数错误 |
| Unauthorized | 认证失败 |
| ConfigurationError | 服务配置错误 |
| ExternalServiceError | 外部服务错误（如SMTP超时） |
| ServerError | 服务器内部错误 |

---

## 调用示例

### JavaScript (Fetch)

```javascript
const sendEmail = async () => {
  const response = await fetch('https://notify.downf.cn/api/email/send', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer nh-prod-Y63jT32xDaxWy1Ek******'
    },
    body: JSON.stringify({
      to: ['user@example.com'],
      subject: '测试邮件',
      body: '这是一封测试邮件',
      category: 'notification',
      isHtml: false
    })
  });

  const result = await response.json();
  console.log(result);
};
```

### Python (Requests)

```python
import requests

url = 'https://notify.downf.cn/api/email/send'
headers = {
    'Content-Type': 'application/json',
    'Authorization': 'Bearer nh-prod-Y63jT32xDaxWy1Ek******'
}
data = {
    'to': ['user@example.com'],
    'subject': '测试邮件',
    'body': '这是一封测试邮件',
    'category': 'notification',
    'isHtml': False
}

response = requests.post(url, json=data, headers=headers)
print(response.json())
```

### C# (HttpClient)

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;

var client = new HttpClient();
client.DefaultRequestHeaders.Add("Authorization", "Bearer nh-prod-Y63jT32xDaxWy1Ek******");

var request = new
{
    to = new[] { "user@example.com" },
    subject = "测试邮件",
    body = "这是一封测试邮件",
    category = "notification",
    isHtml = false
};

var json = JsonSerializer.Serialize(request);
var content = new StringContent(json, Encoding.UTF8, "application/json");

var response = await client.PostAsync("https://notify.downf.cn/api/email/send", content);
var result = await response.Content.ReadAsStringAsync();
Console.WriteLine(result);
```

### PHP (cURL)

```php
<?php
$url = 'https://notify.downf.cn/api/email/send';
$data = [
    'to' => ['user@example.com'],
    'subject' => '测试邮件',
    'body' => '这是一封测试邮件',
    'category' => 'notification',
    'isHtml' => false
];

$ch = curl_init($url);
curl_setopt($ch, CURLOPT_POST, 1);
curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($data));
curl_setopt($ch, CURLOPT_HTTPHEADER, [
    'Content-Type: application/json',
    'Authorization: Bearer nh-prod-Y63jT32xDaxWy1Ek******'
]);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);

$response = curl_exec($ch);
curl_close($ch);

echo $response;
?>
```

---

## 注意事项

1. **安全性**
   - 所有请求必须使用 HTTPS
   - API 密钥请妥善保管，不要在前端代码中硬编码
   - 不支持在 URL 中传递 API 密钥

2. **频率限制**
   - 所有端点：60次/分钟，1000次/小时
   - 邮件发送：30次/分钟
   - 本地IP：200次/分钟
   - 超出限制返回 429 状态码

3. **收件人限制**
   - 单次请求收件人总数（to + cc + bcc）不超过 100 个
   - 邮箱格式必须符合标准规范

4. **内容限制**
   - 邮件主题最大 500 字符
   - 邮件正文最大 50,000 字符
   - 分类标识最大 100 字符

5. **附件说明**
   - 支持常见文件类型：PDF、Office 文档、图片、压缩包、文本文件等
   - 附件使用 Base64 编码传输
   - 支持中文文件名（UTF-8 编码）
   - 禁止可执行文件以确保安全

---

## 技术支持

如遇问题，请联系技术支持团队。

**更新日期**：2025-09-22
