#!/bin/bash
# 生产环境配置文件部署脚本
# 用途: 快速更新 downf.cn 服务器的配置文件
# 使用: bash deploy-configs.sh

set -e  # 遇到错误立即退出

echo "=========================================="
echo "  FileHost 配置文件部署脚本"
echo "=========================================="
echo ""

# 服务器地址
SERVER="root@downf.cn"

# 颜色输出
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# 检查是否在 linux 目录
if [ ! -f "downf.cn.conf" ]; then
    echo -e "${RED}错误: 请在 linux 目录下运行此脚本${NC}"
    exit 1
fi

echo -e "${YELLOW}[1/5] 上传配置文件到服务器...${NC}"
scp downf.cn.conf $SERVER:/tmp/
scp filehost.service $SERVER:/tmp/
scp filehostapi.service $SERVER:/tmp/
echo -e "${GREEN}✓ 配置文件上传完成${NC}"
echo ""

echo -e "${YELLOW}[2/5] 连接服务器并备份旧配置...${NC}"
ssh $SERVER << 'EOF'
    # 备份 nginx 配置
    if [ -f /etc/nginx/conf.d/downf.cn.conf ]; then
        sudo cp /etc/nginx/conf.d/downf.cn.conf /etc/nginx/conf.d/downf.cn.conf.bak.$(date +%Y%m%d_%H%M%S)
        echo "✓ nginx 配置已备份"
    fi

    # 备份 systemd 配置
    if [ -f /etc/systemd/system/filehost.service ]; then
        sudo cp /etc/systemd/system/filehost.service /etc/systemd/system/filehost.service.bak.$(date +%Y%m%d_%H%M%S)
        echo "✓ filehost.service 已备份"
    fi

    if [ -f /etc/systemd/system/filehostapi.service ]; then
        sudo cp /etc/systemd/system/filehostapi.service /etc/systemd/system/filehostapi.service.bak.$(date +%Y%m%d_%H%M%S)
        echo "✓ filehostapi.service 已备份"
    fi
EOF
echo -e "${GREEN}✓ 备份完成${NC}"
echo ""

echo -e "${YELLOW}[3/5] 替换配置文件...${NC}"
ssh $SERVER << 'EOF'
    # 替换 nginx 配置
    sudo cp /tmp/downf.cn.conf /etc/nginx/conf.d/downf.cn.conf
    echo "✓ nginx 配置已更新"

    # 替换 systemd 配置
    sudo cp /tmp/filehost.service /etc/systemd/system/
    sudo cp /tmp/filehostapi.service /etc/systemd/system/
    echo "✓ systemd 配置已更新"
EOF
echo -e "${GREEN}✓ 配置替换完成${NC}"
echo ""

echo -e "${YELLOW}[4/5] 测试 nginx 配置...${NC}"
ssh $SERVER << 'EOF'
    sudo nginx -t
EOF
echo -e "${GREEN}✓ nginx 配置测试通过${NC}"
echo ""

echo -e "${YELLOW}[5/5] 重载服务...${NC}"
ssh $SERVER << 'EOF'
    # 重载 nginx
    sudo systemctl reload nginx
    echo "✓ nginx 已重载"

    # 重载 systemd
    sudo systemctl daemon-reload
    echo "✓ systemd 已重载"

    # 重启服务（如果需要）
    read -p "是否重启 filehost 服务？(y/N) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        sudo systemctl restart filehost filehostapi
        echo "✓ 服务已重启"
    fi

    # 显示状态
    echo ""
    echo "服务状态:"
    sudo systemctl status filehost filehostapi --no-pager -l
EOF
echo ""

echo -e "${GREEN}=========================================="
echo -e "  部署完成！"
echo -e "==========================================${NC}"
echo ""
echo "检查服务状态:"
echo "  ssh $SERVER 'sudo systemctl status filehost filehostapi'"
echo ""
echo "查看日志:"
echo "  ssh $SERVER 'sudo journalctl -u filehost -f'"
echo "  ssh $SERVER 'sudo journalctl -u filehostapi -f'"
echo ""
