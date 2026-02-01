#!/bin/bash

# 一键推送所有分支到所有远程仓库的脚本
# 首先推送到 origin，然后推送到所有其他远程仓库

echo "开始推送所有分支到所有远程仓库..."

# 检查是否在git仓库中
if ! git rev-parse --git-dir > /dev/null 2>&1; then
    echo "错误: 当前目录不是git仓库!"
    exit 1
fi

# 推送到 origin (如果存在)
if git remote | grep -q "^origin$"; then
    echo "正在推送到 origin..."
    if git push origin --all; then
        echo "成功推送到 origin"
    else
        echo "推送到 origin 失败"
    fi
else
    echo "警告: 未找到名为 origin 的远程仓库"
fi

# 获取所有远程仓库列表（排除 origin）
remotes=$(git remote | grep -v "^origin$")

# 如果没有其他远程仓库，则退出
if [ -z "$remotes" ]; then
    echo "没有其他远程仓库需要推送"
    exit 0
fi

# 遍历所有其他远程仓库并推送
for remote in $remotes; do
    echo ""
    echo "正在推送到远程仓库: $remote"
    
    # 执行推送命令
    if git push "$remote" --all; then
        echo "成功推送到 $remote"
    else
        echo "推送到 $remote 失败"
    fi
done

echo ""
echo "所有推送操作完成！"