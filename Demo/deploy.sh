#!/bin/bash

# ========== 配置区域（请修改这三个变量）==========
APP_NAME="Demo"        # 你的应用名称（随便起，用于标识进程）
APP_PORT="5000"               # 应用运行的端口
DLL_NAME="Demo.dll"    # 你的入口dll名称（一般是项目名称.dll）
# ============================================

# 应用发布目录（脚本会自动获取当前目录）
PUBLISH_DIR=$(pwd)
DLL_PATH="$PUBLISH_DIR/$DLL_NAME"

# 颜色输出函数
red() { echo -e "\033[31m$1\033[0m"; }
green() { echo -e "\033[32m$1\033[0m"; }
yellow() { echo -e "\033[33m$1\033[0m"; }

# 检查dll文件是否存在
if [ ! -f "$DLL_PATH" ]; then
    red "错误：找不到 $DLL_PATH"
    red "请确认 DLL_NAME 配置正确，且当前目录包含发布的文件"
    exit 1
fi

green "================================="
green "应用名：$APP_NAME"
green "端口：$APP_PORT"
green "目录：$PUBLISH_DIR"
green "启动文件：$DLL_PATH"
green "================================="

# 停止旧应用
stop_app() {
    yellow "正在停止旧应用：$APP_NAME..."
    # 查找并杀死占用端口的进程（更可靠）
    PID=$(lsof -t -i:$APP_PORT)
    if [ -n "$PID" ]; then
        kill -9 $PID
        green "已终止端口 $APP_PORT 上的进程 PID: $PID"
    else
        # 备用方案：按进程名查找
        PID=$(ps -ef | grep "$DLL_NAME" | grep -v grep | awk '{print $2}')
        if [ -n "$PID" ]; then
            kill -9 $PID
            green "已终止应用进程 PID: $PID"
        else
            yellow "没有找到正在运行的应用"
        fi
    fi
}

# 启动新应用
start_app() {
    yellow "正在启动新应用..."
    
    # 使用 nohup 后台运行，输出日志到当前目录的 app.log
    nohup dotnet "$DLL_PATH" --urls="http://*:$APP_PORT" > "$PUBLISH_DIR/app.log" 2>&1 &
    
    # 保存进程ID
    echo $! > "$PUBLISH_DIR/app.pid"
    green "应用已启动，PID: $(cat $PUBLISH_DIR/app.pid)"
}

# 健康检查
health_check() {
    yellow "正在进行健康检查..."
    sleep 5  # 等待5秒让应用启动
    
    # 尝试访问健康检查接口（请确保你的项目中有 /HealthChecks 接口）
    for i in {1..10}; do
        HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:$APP_PORT/HealthChecks)
        if [ "$HTTP_CODE" -eq 200 ]; then
            green "✅ 健康检查通过！应用已正常运行"
            green "访问地址：http://<你的服务器IP>:$APP_PORT"
            return 0
        fi
        yellow "等待应用启动... ($i/10)"
        sleep 2
    done
    
    red "❌ 健康检查失败，请查看日志：$PUBLISH_DIR/app.log"
    tail -20 "$PUBLISH_DIR/app.log"
    return 1
}

# 查看日志
show_logs() {
    if [ -f "$PUBLISH_DIR/app.log" ]; then
        yellow "最近的日志内容："
        tail -10 "$PUBLISH_DIR/app.log"
    fi
}

# 主流程
main() {
    case "$1" in
        restart)
            stop_app
            start_app
            health_check
            show_logs
            ;;
        stop)
            stop_app
            ;;
        start)
            start_app
            ;;
        status)
            if [ -f "$PUBLISH_DIR/app.pid" ]; then
                PID=$(cat "$PUBLISH_DIR/app.pid")
                if ps -p $PID > /dev/null; then
                    green "应用运行中，PID: $PID"
                else
                    red "应用未运行"
                fi
            else
                red "应用未运行"
            fi
            ;;
        logs)
            show_logs
            ;;
        *)
            echo "用法: $0 {restart|stop|start|status|logs}"
            exit 1
            ;;
    esac
}

main "$@"