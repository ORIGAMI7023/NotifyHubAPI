#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
NotifyHubAPI 项目代码打包脚本
用于生成适合上传到Claude的邮件通知API代码文档
"""

import os
import datetime

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
    output_file = r"D:\Programing\C#\NotifyHubAPI\_NotifyHubAPI_Code.txt"
    
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

    print("开始打包 NotifyHubAPI 项目...")

    with open(output_file, "w", encoding="utf-8") as out:
        # 写入项目描述
        out.write("# NotifyHubAPI - 邮件通知API服务\n")
        out.write("## 项目概述\n")
        out.write("独立的邮件通知API服务，基于ASP.NET Core Web API开发\n")
        out.write("为多个项目提供统一的邮件发送功能，支持多租户、重试机制、状态跟踪等功能\n\n")
        
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
                    # 移除文件末尾多余的空行
                    content = content.rstrip() + "\n"
                    out.write(content)
                
                out.write("```\n")
                
            except Exception as e:
                out.write(f"\n#### 文件: {file_info['path']} [读取失败: {str(e)}]\n")
                out.write("```\n[文件读取失败]\n```\n")

    print(f"NotifyHubAPI 已打包到: {output_file}")
    print(f"包含 {file_count} 个文件，总大小: {get_file_size_from_bytes(total_size)}")
    print("\n打包完成！可以将生成的文件上传到Claude进行代码分析。")

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