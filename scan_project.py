#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
文件夹结构遍历脚本
用于打印当前目录下的所有文件和文件夹信息
"""

import os
import datetime
from pathlib import Path

def format_size(size_bytes):
    """格式化文件大小显示"""
    if size_bytes == 0:
        return "0 B"
    
    size_names = ["B", "KB", "MB", "GB", "TB"]
    i = 0
    while size_bytes >= 1024 and i < len(size_names) - 1:
        size_bytes /= 1024.0
        i += 1
    
    return f"{size_bytes:.1f} {size_names[i]}"

def get_file_info(file_path):
    """获取文件详细信息"""
    try:
        stat = file_path.stat()
        return {
            'size': stat.st_size,
            'modified': datetime.datetime.fromtimestamp(stat.st_mtime),
            'is_dir': file_path.is_dir(),
            'is_file': file_path.is_file()
        }
    except (OSError, PermissionError):
        return {
            'size': 0,
            'modified': None,
            'is_dir': False,
            'is_file': False,
            'error': True
        }

def scan_directory(root_path, max_depth=10, current_depth=0):
    """递归扫描目录结构"""
    items = []
    
    if current_depth >= max_depth:
        return items
    
    try:
        root = Path(root_path)
        for item in sorted(root.iterdir(), key=lambda x: (x.is_file(), x.name.lower())):
            # 跳过隐藏文件和系统文件
            if item.name.startswith('.'):
                continue
                
            # 跳过常见的临时文件和缓存目录
            skip_patterns = {
                '__pycache__', 'node_modules', '.vs', '.vscode', 
                'bin', 'obj', 'Debug', 'Release', '.git'
            }
            if item.name in skip_patterns:
                continue
            
            info = get_file_info(item)
            
            item_data = {
                'name': item.name,
                'path': str(item.relative_to(root_path)),
                'full_path': str(item),
                'depth': current_depth,
                'info': info
            }
            
            items.append(item_data)
            
            # 如果是目录，递归扫描
            if info['is_dir'] and not info.get('error'):
                sub_items = scan_directory(item, max_depth, current_depth + 1)
                items.extend(sub_items)
                
    except PermissionError:
        print(f"权限错误: 无法访问 {root_path}")
    except Exception as e:
        print(f"扫描错误: {e}")
    
    return items

def print_tree_structure(items, show_details=True):
    """打印树状结构"""
    print("=" * 80)
    print("文件夹结构扫描结果")
    print("=" * 80)
    
    for item in items:
        # 计算缩进
        indent = "  " * item['depth']
        
        # 文件/文件夹图标
        if item['info']['is_dir']:
            icon = "📁"
            size_info = ""
        else:
            icon = "📄"
            size_info = f" ({format_size(item['info']['size'])})" if show_details else ""
        
        # 修改时间
        time_info = ""
        if show_details and item['info']['modified']:
            time_info = f" - {item['info']['modified'].strftime('%Y-%m-%d %H:%M')}"
        
        # 打印项目
        if item['info'].get('error'):
            print(f"{indent}❌ {item['name']} [访问错误]")
        else:
            print(f"{indent}{icon} {item['name']}{size_info}{time_info}")

def print_summary(items):
    """打印统计摘要"""
    total_files = sum(1 for item in items if item['info']['is_file'])
    total_dirs = sum(1 for item in items if item['info']['is_dir'])
    total_size = sum(item['info']['size'] for item in items if item['info']['is_file'])
    
    print("\n" + "=" * 80)
    print("扫描统计摘要")
    print("=" * 80)
    print(f"总文件数: {total_files}")
    print(f"总文件夹数: {total_dirs}")
    print(f"总大小: {format_size(total_size)}")

def main():
    """主函数"""
    print("开始扫描当前目录...")
    
    # 获取当前工作目录
    current_dir = Path.cwd()
    print(f"扫描路径: {current_dir}")
    
    # 扫描目录
    items = scan_directory(current_dir, max_depth=8)
    
    # 打印结果
    print_tree_structure(items, show_details=True)
    print_summary(items)
    
    # 生成项目结构文本
    print("\n" + "=" * 80)
    print("项目结构 (复制友好格式)")
    print("=" * 80)
    
    for item in items:
        indent = "  " * item['depth']
        prefix = "├── " if item['depth'] > 0 else ""
        
        if item['info']['is_dir']:
            print(f"{indent}{prefix}{item['name']}/")
        else:
            size_info = f" ({format_size(item['info']['size'])})"
            print(f"{indent}{prefix}{item['name']}{size_info}")

if __name__ == "__main__":
    main()