# NotifyHub API

A secure, high-performance email notification service built with ASP.NET Core 8.0, providing centralized email delivery with enterprise-grade security features.

## Overview

NotifyHub API is a production-ready RESTful service designed to handle email notifications for multiple applications through a unified interface. It features robust security middleware, rate limiting, comprehensive logging, and easy deployment on both Windows and Linux platforms.

## Key Features

### 🔐 Security
- **API Key Authentication** - Header-based authentication (Authorization Bearer or X-API-Key)
- **Advanced Threat Detection** - Protection against Log4Shell, XSS, SQL injection, path traversal, and command injection
- **IP Banning** - Automatic IP blocking with configurable duration
- **Scanner Detection** - Identifies and blocks security scanning tools (sqlmap, nmap, Burp Suite, etc.)
- **Security Headers** - HSTS, CSP, X-Frame-Options, X-Content-Type-Options
- **TLS Enforcement** - HTTPS required by default
- **Privacy Protection** - Email address masking in logs (`u***@example.com`)

### ⚡ Performance & Reliability
- **Rate Limiting** - Per-IP and per-endpoint request throttling
  - General: 20 req/min, 200 req/hour
  - Email sending: 5 req/min, 50 req/hour
- **Asynchronous Processing** - Non-blocking email operations
- **Structured Logging** - Serilog with file rotation and filtering
- **Health Checks** - Built-in endpoint for monitoring

### 📧 Email Features
- **Multiple Recipients** - Support for To, Cc, and Bcc fields (up to 100 recipients)
- **HTML & Plain Text** - Both formats supported
- **SMTP Flexibility** - Works with any SMTP provider (QQ Mail, Gmail, SendGrid, etc.)
- **Email Logging** - Detailed tracking with privacy-aware masking
- **Request Validation** - Comprehensive input validation and sanitization

## Technology Stack

- **Framework**: ASP.NET Core 8.0 (LTS)
- **Email**: MailKit 4.7.1 + MimeKit 4.7.1
- **Logging**: Serilog 8.0.0
- **Rate Limiting**: AspNetCoreRateLimit 5.0.0
- **Database**: SQLite (lightweight, file-based)
- **Documentation**: Swagger/OpenAPI

## Quick Start

### Prerequisites

- .NET 8.0 Runtime or SDK
- SMTP server credentials (e.g., QQ Mail, Gmail)
- Linux: Nginx (for reverse proxy)

### Installation

#### Option 1: Development Environment

```bash
# Clone the repository
git clone <repository-url>
cd NotifyHubAPI

# Configure SMTP settings (use environment variables or appsettings.Development.json)
export NOTIFYHUB_SMTP_HOST="smtp.qq.com"
export NOTIFYHUB_SMTP_USERNAME="your-email@qq.com"
export NOTIFYHUB_SMTP_PASSWORD="your-auth-code"
export NOTIFYHUB_SMTP_FROMEMAIL="your-email@qq.com"
export NOTIFYHUB_APIKEY_DEFAULT="your-secure-api-key"

# Run the application
cd NotifyHubAPI
dotnet run
```

The API will be available at `http://localhost:5002`

#### Option 2: Production Deployment (Linux)

See [Linux Deployment Guide](linux/LINUX_DEPLOYMENT_GUIDE.md) for complete instructions.

### Configuration

Configuration is managed through environment variables (recommended) or `appsettings.json`:

**Required Environment Variables:**
```bash
NOTIFYHUB_SMTP_HOST=smtp.qq.com
NOTIFYHUB_SMTP_PORT=587
NOTIFYHUB_SMTP_USESSL=true
NOTIFYHUB_SMTP_USERNAME=your-email@qq.com
NOTIFYHUB_SMTP_PASSWORD=your-smtp-password
NOTIFYHUB_SMTP_FROMEMAIL=your-email@qq.com
NOTIFYHUB_SMTP_FROMNAME=NotifyHub
NOTIFYHUB_APIKEY_DEFAULT=your-api-key-here
```

**Optional Environment Variables:**
```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:5002
```

## API Usage

### Authentication

All API requests require authentication via API key in the request header:

```http
Authorization: Bearer YOUR_API_KEY
```

or

```http
X-API-Key: YOUR_API_KEY
```

### Send Email

**Endpoint:** `POST /api/email/send`

**Request Body:**
```json
{
  "to": ["recipient@example.com"],
  "subject": "Test Email",
  "bodyHtml": "<h1>Hello World</h1>",
  "bodyText": "Hello World",
  "cc": ["cc@example.com"],
  "bcc": ["bcc@example.com"]
}
```

**Response (Success):**
```json
{
  "success": true,
  "message": "邮件发送成功",
  "data": {
    "emailId": "550e8400-e29b-41d4-a716-446655440000",
    "timestamp": "2024-01-15T10:30:00Z"
  }
}
```

**Response (Error):**
```json
{
  "success": false,
  "message": "邮件发送失败",
  "error": "SMTP connection failed"
}
```

### Health Check

**Endpoint:** `GET /health`

Returns service health status including SMTP connectivity and API key configuration.

## Security Best Practices

1. **Never commit credentials** - Use environment variables for all sensitive data
2. **Rotate API keys regularly** - Generate new keys periodically
3. **Use HTTPS** - Always deploy behind TLS/SSL
4. **Monitor logs** - Check `logs/security-scan.log` for suspicious activity
5. **Update dependencies** - Keep packages up to date
6. **Limit access** - Use firewall rules to restrict API access

## Project Structure

```
NotifyHubAPI/
├── Controllers/          # API endpoints
│   └── EmailController.cs
├── Middleware/           # Security & validation middleware
│   ├── ApiKeyMiddleware.cs
│   ├── EnhancedSecurityMiddleware.cs
│   ├── GlobalExceptionMiddleware.cs
│   └── ...
├── Services/             # Business logic
│   ├── EmailService.cs
│   └── ApiKeyService.cs
├── Models/               # Data models
│   ├── EmailRequest.cs
│   └── StandardApiResponse.cs
├── Tests/                # Test utilities
└── Program.cs            # Application entry point

linux/                    # Linux deployment configurations
├── notifyhub.service     # systemd service file
├── notify.downf.cn.conf  # Nginx configuration
├── LINUX_DEPLOYMENT_GUIDE.md
└── CHANGELOG.md
```

## Deployment

### Linux (systemd + Nginx)

Comprehensive deployment guide available at [linux/LINUX_DEPLOYMENT_GUIDE.md](linux/LINUX_DEPLOYMENT_GUIDE.md)

**Quick Summary:**
1. Install .NET 8.0 Runtime and Nginx
2. Deploy application to `/var/www/notifyhub`
3. Configure systemd service with environment variables
4. Set up Nginx reverse proxy with SSL
5. Enable and start services

### Docker (Coming Soon)

Docker support is planned for a future release.

## Monitoring & Logs

### Log Files

- **Application logs**: `logs/notifyhub.log` (7 days retention, 50MB max)
- **Email logs**: `logs/email-sent-{date}.log` (permanent)
- **Security logs**: `logs/security-scan.log` (3 days retention)

### Systemd Journal (Linux)

```bash
# View live logs
sudo journalctl -u notifyhub -f

# View recent logs
sudo journalctl -u notifyhub -n 100
```

### Health Monitoring

```bash
# Check service health
curl https://notify.downf.cn/health

# Check service status (Linux)
sudo systemctl status notifyhub
```

## Rate Limits

| Endpoint | Per Minute | Per Hour |
|----------|------------|----------|
| All endpoints | 20 | 200 |
| `/api/email/send` | 5 | 50 |
| Localhost | 100 | - |

## Development

### Building from Source

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests (coming soon)
dotnet test

# Publish for deployment
dotnet publish -c Release -o publish
```

### API Documentation

When running in Development mode, Swagger UI is available at:
- `http://localhost:5002/swagger`

## Troubleshooting

### Common Issues

**SMTP Connection Failed**
- Verify SMTP credentials and firewall rules
- For QQ Mail, ensure you're using the authorization code (not password)
- Check port 587 is accessible

**502 Bad Gateway (Nginx)**
- Ensure application is running: `sudo systemctl status notifyhub`
- Check port configuration matches in service and Nginx config
- Verify firewall allows local port 5002

**Rate Limit Exceeded**
- Check IP rate limiting configuration
- Review logs for actual request counts
- Consider whitelisting trusted IPs

See [linux/LINUX_DEPLOYMENT_GUIDE.md](linux/LINUX_DEPLOYMENT_GUIDE.md) for more troubleshooting steps.

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

[Specify your license here]

## Support

For issues, questions, or feature requests, please:
- Open an issue on GitHub
- Check the [Changelog](linux/CHANGELOG.md) for recent updates
- Review the [Deployment Guide](linux/LINUX_DEPLOYMENT_GUIDE.md)

## Changelog

See [CHANGELOG.md](linux/CHANGELOG.md) for a detailed list of changes and updates.

---

**Current Version**: 1.0.0
**Last Updated**: 2024-11-23
**Target Platform**: .NET 8.0 (Linux/Windows)
