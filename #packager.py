#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
NotifyHubAPI 项目代码打包脚本
用于生成适合上传到Claude的邮件通知API代码文档
✨ 包含敏感信息保护功能
"""

import os
import datetime
import json
import re

# ✨敏感信息保护功能
def mask_value(val: str, min_show: int = None) -> str:
    """
    根据字符串长度智能掩码
    - 短值（≤8字符）：显示前4位 + ****
    - 中等值（9-16字符）：显示前8位 + ****
    - 长值（>16字符）：显示前16位 + ****
    """
    if not val:
        return val
    
    if min_show is not None:
        show_chars = min_show
    elif len(val) <= 8:
        show_chars = min(4, len(val))
    elif len(val) <= 16:
        show_chars = 8
    else:
        show_chars = 16
    
    if len(val) <= show_chars:
        return val
    return val[:show_chars] + '****'

# ✨敏感信息关键词列表
SENSITIVE_KEYWORDS = [
    'password', 'pwd', 'passwd', 'secret', 'key', 'token', 'apikey', 'api_key',
    'connectionstring', 'connstr', 'connection_string', 'hash', 'salt', 
    'signature', 'private', 'credential', 'auth', 'jwt', 'bearer',
    'database', 'server', 'userid', 'user_id', 'username', 'smtp', 'mail'
]

def is_sensitive_key(key: str) -> bool:
    """检查键名是否包含敏感关键词"""
    key_lower = key.lower()
    return any(keyword in key_lower for keyword in SENSITIVE_KEYWORDS)

def mask_connection_string(conn_str: str) -> str:
    """智能处理连接字符串，只掩码敏感部分"""
    if not conn_str:
        return conn_str
    
    # 处理各种连接字符串格式
    patterns = [
        (r'(password|pwd)\s*=\s*([^;]+)', r'\1=****'),
        (r'(user\s*id|uid|username)\s*=\s*([^;]+)', lambda m: f'{m.group(1)}={mask_value(m.group(2), 4)}'),
        (r'(server|data\s*source)\s*=\s*([^;]+)', lambda m: f'{m.group(1)}={mask_value(m.group(2), 8)}'),
    ]
    
    result = conn_str
    for pattern, replacement in patterns:
        if callable(replacement):
            result = re.sub(pattern, replacement, result, flags=re.IGNORECASE)
        else:
            result = re.sub(pattern, replacement, result, flags=re.IGNORECASE)
    
    return result

def process_json_content(content: str) -> str:
    """处理JSON文件中的敏感信息"""
    try:
        data = json.loads(content)
        processed_data = mask_json_recursive(data)
        return json.dumps(processed_data, indent=2, ensure_ascii=False)
    except json.JSONDecodeError:
        return content

def mask_json_recursive(obj):
    """递归处理JSON对象中的敏感信息"""
    if isinstance(obj, dict):
        result = {}
        for key, value in obj.items():
            if isinstance(value, str) and is_sensitive_key(key):
                # 特殊处理连接字符串
                if 'connection' in key.lower():
                    result[key] = mask_connection_string(value)
                else:
                    result[key] = mask_value(value)
            elif isinstance(value, (dict, list)):
                result[key] = mask_json_recursive(value)
            else:
                result[key] = value
        return result
    elif isinstance(obj, list):
        return [mask_json_recursive(item) for item in obj]
    else:
        return obj

def process_csharp_content(content: str) -> str:
    """处理C#代码中的敏感信息"""
    lines = content.split('\n')
    processed_lines = []
    
    for line in lines:
        # 处理字符串字面量赋值
        # 如: string password = "secret123";
        string_assignment_pattern = r'(\w*(?:' + '|'.join(SENSITIVE_KEYWORDS) + r')\w*)\s*=\s*["\']([^"\']+)["\']'
        
        def replace_assignment(match):
            var_name, value = match.groups()
            if is_sensitive_key(var_name):
                return f'{var_name} = "{mask_value(value)}"'
            return match.group(0)
        
        processed_line = re.sub(string_assignment_pattern, replace_assignment, line, flags=re.IGNORECASE)
        
        # 处理常量定义
        # 如: const string API_KEY = "abc123";
        const_pattern = r'(const\s+string\s+\w*(?:' + '|'.join(SENSITIVE_KEYWORDS) + r')\w*\s*=\s*["\'])([^"\']+)(["\'])'
        
        def replace_const(match):
            prefix, value, suffix = match.groups()
            return f'{prefix}{mask_value(value)}{suffix}'
        
        processed_line = re.sub(const_pattern, replace_const, processed_line, flags=re.IGNORECASE)
        
        # 处理配置访问
        # 如: Configuration["ConnectionStrings:Default"]
        config_pattern = r'(Configuration\[["\'][^"\']*(?:' + '|'.join(SENSITIVE_KEYWORDS) + r')[^"\']*["\']]\s*=\s*["\'])([^"\']+)(["\'])'
        
        def replace_config(match):
            prefix, value, suffix = match.groups()
            return f'{prefix}{mask_value(value)}{suffix}'
        
        processed_line = re.sub(config_pattern, replace_config, processed_line, flags=re.IGNORECASE)
        
        processed_lines.append(processed_line)
    
    return '\n'.join(processed_lines)

def process_file_content(file_path: str, content: str) -> str:
    """根据文件类型处理敏感信息"""
    file_ext = os.path.splitext(file_path)[1].lower()
    file_name = os.path.basename(file_path).lower()
    
    if file_ext == '.json':
        # 处理 JSON 配置文件
        return process_json_content(content)
    elif file_ext == '.cs':
        # 处理 C# 代码文件
        return process_csharp_content(content)
    elif file_ext in ['.config', '.xml'] and ('web.config' in file_name or 'app.config' in file_name):
        # 处理 XML 配置文件中的环境变量
        def replacer(m):
            name, val = m.group(1), m.group(2)
            return f'<add key="{name}" value="{mask_value(val)}" />'
        
        # 处理 appSettings
        pattern = re.compile(r'<add\s+key="([^"]+)"\s+value="([^"]+)"\s*/>')
        content = pattern.sub(replacer, content)
        
        # 处理 connectionStrings
        def conn_replacer(m):
            name, conn_str = m.group(1), m.group(2)
            return f'<add name="{name}" connectionString="{mask_connection_string(conn_str)}" />'
        
        conn_pattern = re.compile(r'<add\s+name="([^"]+)"\s+connectionString="([^"]+)"[^>]*>')
        content = conn_pattern.sub(conn_replacer, content)
        
        return content
    else:
        return content

def get_file_size_from_bytes(size_bytes):
    """将字节数转换为人类可读格式"""
    if size_bytes < 1024:
        return f"{size_bytes} B"
    elif size_bytes < 1024 * 1024:
        return f"{size_bytes/1024:.1f} KB"
    else:
        return f"{size_bytes/(1024*1024):.1f} MB"

def should_skip_file(filename):
    """判断是否应该跳过文件"""
    skip_patterns = [
        ".tmp", ".temp", ".bak", ".old", ".user", ".suo", ".cache",
        ".dll", ".exe", ".pdb", ".deps.json", ".http"
    ]
    
    skip_files = [
        "AssemblyInfo.cs",
        "GlobalAssemblyInfo.cs",
        "WeatherForecast.cs",
        "WeatherForecastController.cs",
        "NotifyHubAPI.csproj.user"
    ]
    
    # 检查文件扩展名
    for pattern in skip_patterns:
        if filename.lower().endswith(pattern):
            return True
    
    # 检查特定文件名
    if filename in skip_files:
        return True
        
    return False

def main():
    # NotifyHubAPI 项目根目录 - 根据您的截图，实际项目在子文件夹中
    root_dir = r"D:\Programing\C#\NotifyHubAPI\NotifyHubAPI"  # 注意这里多了一层
    output_file = r"D:\Programing\C#\NotifyHubAPI\#NotifyHubAPI_Code.txt"
    
    # 检查目录是否存在
    if not os.path.exists(root_dir):
        print(f"错误: 目录不存在: {root_dir}")
        print("请修改脚本中的 root_dir 路径")
        
        # 尝试父目录
        parent_dir = r"D:\Programing\C#\NotifyHubAPI"
        if os.path.exists(parent_dir):
            print(f"发现父目录: {parent_dir}")
            print("父目录内容:")
            for item in os.listdir(parent_dir):
                item_path = os.path.join(parent_dir, item)
                if os.path.isdir(item_path):
                    print(f"  📁 {item}/")
                else:
                    print(f"  📄 {item}")
        return

    # 根目录允许的文件扩展名
    root_exts = [".cs", ".json", ".csproj"]
    
    # 需要遍历的子目录
    target_folders = [
        "Models", 
        "Services", 
        "Data",
        "Controllers",
        "Middleware",
        "BackgroundServices",
        "Extensions",
        "Configuration",
        "Utils",
        "Helpers"
    ]

    all_paths = []
    total_size = 0
    file_count = 0
    protected_files = 0  # ✨统计被保护的文件数

    print("开始打包 NotifyHubAPI 项目...")

    with open(output_file, "w", encoding="utf-8") as out:
        # 写入项目描述
        out.write("# NotifyHubAPI - 邮件通知API服务\n")
        out.write("## 项目概述\n")
        out.write("独立的邮件通知API服务，基于ASP.NET Core Web API开发\n")
        out.write("为多个项目提供统一的邮件发送功能，支持多租户、重试机制、状态跟踪等功能\n")
        out.write("⚠️  敏感信息已自动掩码处理，保护密码、密钥、连接字符串等\n\n")
        
        # 先收集所有文件信息
        print(f"扫描目录: {root_dir}")
        
        for dirpath, dirnames, filenames in os.walk(root_dir):
            rel_dir = os.path.relpath(dirpath, root_dir)
            print(f"正在扫描: {rel_dir}")
            
            # 跳过编译输出目录和隐藏目录
            if any(skip_dir in rel_dir for skip_dir in ["bin", "obj", "publish", ".vs", ".git"]):
                print(f"跳过目录: {rel_dir}")
                continue
            
            # 检查是否是目标文件夹 - 修复逻辑
            is_target_folder = False
            if rel_dir == ".":
                # 根目录总是处理
                is_target_folder = True
            else:
                # 检查是否是目标子文件夹
                for target_folder in target_folders:
                    if rel_dir == target_folder or rel_dir.startswith(target_folder + os.sep):
                        is_target_folder = True
                        break
            
            if not is_target_folder:
                print(f"跳过非目标目录: {rel_dir}")
                continue
                
            print(f"处理目录: {rel_dir}, 文件数: {len(filenames)}")
                
            for filename in filenames:
                print(f"  检查文件: {filename}")
                
                if should_skip_file(filename):
                    print(f"    跳过文件: {filename}")
                    continue
                    
                ext = os.path.splitext(filename)[1].lower()
                print(f"    文件扩展名: {ext}")
                
                # 根目录允许配置文件和项目文件，子目录主要是源码
                include_file = False
                if rel_dir == ".":
                    include_file = ext in root_exts
                    print(f"    根目录文件，是否包含: {include_file}")
                else:
                    include_file = ext in [".cs", ".json"]
                    print(f"    子目录文件，是否包含: {include_file}")
                
                if include_file:
                    file_path = os.path.join(dirpath, filename)
                    rel_path = os.path.relpath(file_path, root_dir)
                    file_size = os.path.getsize(file_path)
                    
                    all_paths.append({
                        'path': rel_path,
                        'full_path': file_path,
                        'size': file_size,
                        'folder': rel_dir
                    })
                    total_size += file_size
                    file_count += 1
                    print(f"    ✓ 已添加: {rel_path} ({get_file_size_from_bytes(file_size)})")
                else:
                    print(f"    ✗ 不包含: {filename}")
        
        print(f"\n找到 {file_count} 个文件进行打包")

        # 按文件夹和文件名排序
        all_paths.sort(key=lambda x: (x['folder'], x['path']))

        # 写入文件索引
        out.write("## 文件索引\n")
        current_folder = ""
        for file_info in all_paths:
            folder = file_info['folder']
            if folder != current_folder:
                current_folder = folder
                folder_name = "根目录" if folder == "." else folder
                out.write(f"\n### {folder_name}\n")
            
            size_str = get_file_size_from_bytes(file_info['size'])
            out.write(f"- {file_info['path']} ({size_str})\n")
        
        # 写入项目统计
        out.write(f"\n## 项目统计\n")
        out.write(f"- 总文件数: {file_count}\n")
        out.write(f"- 总大小: {get_file_size_from_bytes(total_size)}\n")
        out.write(f"- 生成时间: {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        out.write(f"- 项目路径: {root_dir}\n")
        
        # 写入技术栈信息
        out.write(f"\n## 技术栈\n")
        out.write("- ASP.NET Core 8.0 Web API\n")
        out.write("- Entity Framework Core (SQL Server)\n")
        out.write("- MailKit (邮件发送)\n")
        out.write("- Serilog (日志记录)\n")
        out.write("- AspNetCoreRateLimit (速率限制)\n")
        
        # 写入文件内容
        out.write("\n" + "="*80 + "\n")
        out.write("## 文件内容\n")
        out.write("="*80 + "\n")
        
        current_folder = ""
        for file_info in all_paths:
            folder = file_info['folder']
            if folder != current_folder:
                current_folder = folder
                folder_name = "根目录" if folder == "." else folder
                out.write(f"\n\n### {folder_name} 文件夹\n")
                out.write("-" * 50 + "\n")
            
            try:
                out.write(f"\n#### 文件: {file_info['path']}\n")
                out.write(f"```{get_file_extension_for_syntax(file_info['path'])}\n")
                
                with open(file_info['full_path'], "r", encoding="utf-8", errors="ignore") as f:
                    content = f.read()
                    
                    # ✨处理敏感信息
                    original_content = content
                    content = process_file_content(file_info['path'], content)
                    
                    # 统计是否有敏感信息被保护
                    if content != original_content:
                        protected_files += 1
                    
                    # 移除文件末尾多余的空行
                    content = content.rstrip() + "\n"
                    out.write(content)
                
                out.write("```\n")
                
            except Exception as e:
                out.write(f"\n#### 文件: {file_info['path']} [读取失败: {str(e)}]\n")
                out.write("```\n[文件读取失败]\n```\n")
        
        # ✨写入保护统计
        out.write(f"\n\n## 敏感信息保护统计\n")
        out.write(f"- 受保护文件数: {protected_files}\n")
        out.write("- 保护内容: 密码、密钥、连接字符串、API令牌等\n")
        out.write("- 保护方式: 智能掩码，保留前缀用于识别\n")

    # 计算输出文件大小并显示统计信息
    if os.path.exists(output_file):
        output_size = os.path.getsize(output_file)
        
        print("\n" + "=" * 60)
        print("✅ 所有文件合并成功!")
        print(f"输出文件: {output_file}")
        print(f"文件大小: {get_file_size_from_bytes(output_size)}")
        print(f"包含文件总数: {file_count}")
        print(f"敏感信息保护: {protected_files} 个文件")
        print("=" * 60)
        print("\n✅ 打包完成！敏感信息已自动保护，可以安全上传到Claude进行代码分析。")

def get_file_extension_for_syntax(file_path):
    """根据文件路径返回语法高亮的语言标识"""
    ext = os.path.splitext(file_path)[1].lower()
    
    syntax_map = {
        '.cs': 'csharp',
        '.json': 'json',
        '.csproj': 'xml',
        '.config': 'xml',
        '.xml': 'xml',
        '.txt': 'text',
        '.md': 'markdown'
    }
    
    return syntax_map.get(ext, 'text')

if __name__ == "__main__":
    main()