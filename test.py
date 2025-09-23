#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
NotifyHubAPI 简单邮件测试脚本
发送一封测试邮件验证API功能
"""

import requests
import json
from datetime import datetime

def send_test_email():
    """发送测试邮件"""
    
    # API配置
    base_url = "https://notify.origami7023.cn"
    # api_key = "default-api-key-2024"
    api_key = "nh-prod-7K9mP3xR8wQ2nV5ag4yQWcL8sA9dE3tY7uI2oP6qW1zX5"
    
    # 邮件内容
    email_data = {
        "to": ["2840080918@qq.com"],
        "subject": "Python脚本测试邮件",
        "body": f"这是通过Python脚本发送的测试邮件。\n\n发送时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        "category": "PYTHON_TEST",
        "isHtml": False
    }
    
    # 请求头
    headers = {
        "Content-Type": "application/json",
        "X-API-Key": api_key
    }
    
    try:
        print("正在发送测试邮件...")
        print(f"API地址: {base_url}/api/Email/send")
        print(f"收件人: {email_data['to'][0]}")
        print(f"主题: {email_data['subject']}")
        
        # 发送请求
        response = requests.post(
            f"{base_url}/api/Email/send",
            headers=headers,
            json=email_data,
            timeout=30
        )
        
        # 处理响应
        print(f"\n响应状态码: {response.status_code}")
        
        if response.status_code == 200:
            result = response.json()
            print("✅ 邮件发送成功!")
            print(f"邮件ID: {result['data']['emailId']}")
            print(f"状态: {result['data']['status']}")
            print(f"消息: {result['data']['message']}")
            return result['data']['emailId']
        else:
            print("❌ 邮件发送失败!")
            print(f"错误信息: {response.text}")
            return None
            
    except requests.exceptions.RequestException as e:
        print(f"❌ 网络请求失败: {e}")
        return None
    except json.JSONDecodeError as e:
        print(f"❌ JSON解析失败: {e}")
        return None
    except Exception as e:
        print(f"❌ 未知错误: {e}")
        return None

def main():
    """主函数"""
    print("NotifyHubAPI 邮件测试脚本")
    print("=" * 40)
    
    # 发送测试邮件
    email_id = send_test_email()
    
    if email_id:
        print(f"\n请检查您的邮箱 2840080918@qq.com 是否收到了测试邮件")
        print(f"邮件ID: {email_id}")
    else:
        print("\n测试失败，请检查:")
        print("1. API服务是否运行在 https://notify.origami7023.cn")
        print("2. API Key是否正确")
        print("3. 网络连接是否正常")

if __name__ == "__main__":
    main()