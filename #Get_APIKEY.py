#!/usr/bin/env python3
import secrets
import base64

# 生成 32 字节安全随机数
key_bytes = secrets.token_bytes(32)

# Base64 输出（常用于配置）
key_b64 = base64.b64encode(key_bytes).decode("utf-8")

print(key_b64)
