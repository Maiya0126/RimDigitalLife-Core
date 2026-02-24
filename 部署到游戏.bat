@echo off
REM ============================================================================
REM RimDigitalLife - 部署到 RimWorld Mods 文件夹
REM 【Version: v1.1.0 | 自动部署脚本】
REM @author Maiya0126 (麦丫)
REM ============================================================================

echo.
echo ============================================================
echo  RimDigitalLife - 一键部署工具
echo  版本: v1.1.0
echo  作者: Maiya0126 (麦丫)
echo ============================================================
echo.

REM 设置源路径和目标路径
set SOURCE_DIR=D:\Visual Studio Code ALL\RimDigitalLife
set TARGET_DIR=D:\games\steamapps\common\RimWorld\Mods\MaiyaMod02 RimDigitalLife 边缘数码生活

echo [信息] 源目录: %SOURCE_DIR%
echo [信息] 目标目录: %TARGET_DIR%
echo.

REM 检查源目录是否存在
if not exist "%SOURCE_DIR%" (
    echo [错误] 找不到源目录: %SOURCE_DIR%
    pause
    exit /b 1
)

REM 检查编译输出是否存在
if not exist "%SOURCE_DIR%\Assemblies\RimDigitalLife.dll" (
    echo [错误] 找不到编译输出: RimDigitalLife.dll
    echo 请先编译项目（按 Ctrl+Shift+B 或运行 编译.bat）
    pause
    exit /b 1
)

echo [信息] 开始部署...
echo.

REM 创建目标目录结构
echo [1/6] 创建目录结构...
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
if not exist "%TARGET_DIR%\About" mkdir "%TARGET_DIR%\About"
if not exist "%TARGET_DIR%\Assemblies" mkdir "%TARGET_DIR%\Assemblies"
if not exist "%TARGET_DIR%\Defs" mkdir "%TARGET_DIR%\Defs"
if not exist "%TARGET_DIR%\Textures" mkdir "%TARGET_DIR%\Textures"

REM 复制核心文件
echo [2/6] 复制 Assemblies...
xcopy /Y /Q "%SOURCE_DIR%\Assemblies\*.dll" "%TARGET_DIR%\Assemblies\" >nul

REM 复制 About 文件
echo [3/6] 复制 About.xml...
xcopy /Y /Q "%SOURCE_DIR%\About\About.xml" "%TARGET_DIR%\About\" >nul

REM 复制 Defs 文件夹
echo [4/6] 复制 Defs...
xcopy /Y /Q /E "%SOURCE_DIR%\Defs" "%TARGET_DIR%\Defs\" >nul

REM 复制 Source 文件夹（可选，用于开发调试）
echo [5/6] 复制 Source（可选）...
xcopy /Y /Q /E "%SOURCE_DIR%\Source" "%TARGET_DIR%\Source\" >nul

REM 复制项目文件（可选）
echo [6/6] 复制项目文件...
copy /Y "%SOURCE_DIR%\RimDigitalLife.csproj" "%TARGET_DIR%\" >nul
copy /Y "%SOURCE_DIR%\编译.bat" "%TARGET_DIR%\" >nul

echo.
echo ============================================================
echo [成功] 部署完成！
echo ============================================================
echo.
echo Mod 位置: %TARGET_DIR%
echo.
echo 下一步:
echo   1. 启动 RimWorld
echo   2. 打开 Mod 菜单
echo   3. 找到 "MaiyaMod02 RimDigitalLife 边缘数码生活"
echo   4. 勾选并加载游戏
echo   5. 检查是否正常工作
echo.
echo 注意事项:
echo   - Textures 文件夹中的 PNG 图片需要你手动准备
echo   - 或先运行游戏测试功能，图片可以后续添加
echo.
pause
