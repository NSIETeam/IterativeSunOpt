@echo off
REM ============================================
REM IterativeSunOpt 插件打包脚本
REM ============================================

setlocal enabledelayedexpansion

echo.
echo ============================================
echo   IterativeSunOpt 插件打包工具
echo   Version: 2.0.0
echo ============================================
echo.

REM 设置变量
set PROJECT_NAME=IterativeSunOpt
set VERSION=2.0.0
set DIST_DIR=dist
set PACKAGE_NAME=%PROJECT_NAME%-v%VERSION%

REM 检查是否安装了 .NET Framework 4.8 SDK
echo [1/5] 检查编译环境...
where msbuild >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 未找到 MSBuild，请安装 Visual Studio 或 .NET SDK
    echo 提示: 使用 Visual Studio Developer Command Prompt 运行此脚本
    pause
    exit /b 1
)
echo       环境检查通过

REM 清理旧的编译输出
echo.
echo [2/5] 清理旧的编译输出...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
echo       清理完成

REM 编译项目 (Release 模式)
echo.
echo [3/5] 编译项目 (Release 模式)...
msbuild %PROJECT_NAME%.csproj /p:Configuration=Release /p:Platform=x64 /verbosity:minimal
if %errorlevel% neq 0 (
    echo [错误] 编译失败
    pause
    exit /b 1
)
echo       编译成功

REM 创建发布包
echo.
echo [4/5] 创建发布包...
if not exist %DIST_DIR% mkdir %DIST_DIR%
if exist %DIST_DIR%\%PACKAGE_NAME% rmdir /s /q %DIST_DIR%\%PACKAGE_NAME%
mkdir %DIST_DIR%\%PACKAGE_NAME%

REM 复制文件
copy bin\Release\%PROJECT_NAME%.dll %DIST_DIR%\%PACKAGE_NAME%\ >nul
copy README.md %DIST_DIR%\%PACKAGE_NAME%\ >nul 2>&1
copy LICENSE %DIST_DIR%\%PACKAGE_NAME%\ >nul 2>&1
copy MAINTENANCE.md %DIST_DIR%\%PACKAGE_NAME%\ >nul 2>&1

REM 创建安装说明
echo 创建安装说明...
(
echo IterativeSunOpt v%VERSION% 安装指南
echo ====================================
echo.
echo 1. 系统要求
echo    - Rhino 7 或 Rhino 8
echo    - Windows 10/11
echo    - .NET Framework 4.8
echo.
echo 2. 安装步骤
echo    方法一: 手动安装
echo    1. 将 IterativeSunOpt.dll 复制到以下目录:
echo       %%AppData%%\McNeel\Rhinoceros\8.0\Plug-ins\IterativeSunOpt\
echo    2. 启动 Rhino 8
echo    3. 运行命令: IterativeSunOpt
echo.
echo    方法二: 拖放安装
echo    1. 将 IterativeSunOpt.dll 拖放到 Rhino 视口
echo    2. 在弹出的对话框中确认安装
echo.
echo 3. 可用命令
echo    - IterativeSunOpt : 运行迭代优化
echo    - ShowOptResults  : 显示优化结果
echo    - ConfigMetrics   : 配置评估指标
echo    - SetBuildingType : 设置建筑类型
echo    - SetAIMode       : 设置 AI 模式
echo.
echo 4. 快速开始
echo    1. 在 Rhino 中创建一个简单的建筑体块
echo    2. 运行 IterativeSunOpt 命令
echo    3. 选择体块并设置参数
echo    4. 等待优化完成
echo.
echo 5. 技术支持
echo    - GitHub: https://github.com/your-repo/IterativeSunOpt
echo    - Email: support@example.com
echo.
) > %DIST_DIR%\%PACKAGE_NAME%\INSTALL.txt

echo       发布包创建完成: %DIST_DIR%\%PACKAGE_NAME%\

REM 创建 ZIP 压缩包
echo.
echo [5/5] 创建 ZIP 压缩包...
if exist %DIST_DIR%\%PACKAGE_NAME%.zip del %DIST_DIR%\%PACKAGE_NAME%.zip
powershell -command "Compress-Archive -Path '%DIST_DIR%\%PACKAGE_NAME%\*' -DestinationPath '%DIST_DIR%\%PACKAGE_NAME%.zip'"
if %errorlevel% neq 0 (
    echo [警告] ZIP 压缩失败，请手动压缩
) else (
    echo       ZIP 包创建完成: %DIST_DIR%\%PACKAGE_NAME%.zip
)

REM 完成
echo.
echo ============================================
echo   打包完成!
echo ============================================
echo.
echo 输出目录: %DIST_DIR%\
echo 发布包:   %PACKAGE_NAME%\
echo ZIP包:    %PACKAGE_NAME%.zip
echo.
echo 请将 IterativeSunOpt.dll 复制到 Rhino 插件目录进行安装
echo 或直接将 DLL 文件拖放到 Rhino 视口进行安装
echo.
pause
