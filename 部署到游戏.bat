@echo off
REM ============================================================================
REM RimDigitalLife - 部署到 RimWorld Mods 文件夹 (v2.0 | 无 Source 版)
REM @author Maiya0126 (麦丫)
REM 说明: 将编译产物与资源文件部署到游戏 Mods 文件夹，不包含 Source 源码
REM DLL 来源: RimDigitalLife\bin\Release\RimDigitalLife.dll (请先运行 编译.bat)
REM ============================================================================

echo.
echo ============================================================
echo  RimDigitalLife - 部署工具
echo  版本: v2.0 (无 Source)
echo  作者: Maiya0126 (麦丫)
echo ============================================================
echo.

REM 源码项目目录 (RimDigitalLife 为核心模组项目文件夹)
set SOURCE_DIR=D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife
set DLL_SOURCE=D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife\bin\Release\RimDigitalLife.dll

REM 目标游戏模组文件夹 (修饰为实际文件夹名)
set TARGET_DIR=D:\Games\steamapps\common\RimWorld\Mods\MaiyaMod02 Rim Digital Life Core 边缘数码生活核心版

echo [信息] 源项目目录: %SOURCE_DIR%
echo [信息] 目标模组目录: %TARGET_DIR%
echo.

REM 检查编译产物是否存在
if not exist "%DLL_SOURCE%" (
    echo [错误] 找不到编译产物: RimDigitalLife.dll
    echo 请先运行 编译.bat 生成 DLL
    pause
    exit /b 1
)

REM 检查源目录是否存在
if not exist "%SOURCE_DIR%" (
    echo [错误] 找不到源目录: %SOURCE_DIR%
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
if not exist "%TARGET_DIR%\Languages" mkdir "%TARGET_DIR%\Languages"
if not exist "%TARGET_DIR%\Textures" mkdir "%TARGET_DIR%\Textures"

REM 复制编译产物
echo [2/6] 复制 RimDigitalLife.dll...
copy /Y "%DLL_SOURCE%" "%TARGET_DIR%\Assemblies\" >nul

REM 复制 About
echo [3/6] 复制 About...
xcopy /Y /Q /E "%SOURCE_DIR%\About" "%TARGET_DIR%\About\" >nul

REM 复制 Defs
echo [4/6] 复制 Defs...
xcopy /Y /Q /E "%SOURCE_DIR%\Defs" "%TARGET_DIR%\Defs\" >nul

REM 复制 Languages
echo [5/6] 复制 Languages...
xcopy /Y /Q /E "%SOURCE_DIR%\Languages" "%TARGET_DIR%\Languages\" >nul

REM 复制 Textures
echo [6/6] 复制 Textures...
xcopy /Y /Q /E "%SOURCE_DIR%\Textures" "%TARGET_DIR%\Textures\" >nul

echo.
echo ============================================================
echo [成功] 部署完成！(未包含 Source 源码)
echo ============================================================
echo.
echo Mod 位置: %TARGET_DIR%
echo.
echo 下一步: 启动 RimWorld，在 Mod 菜单勾选并加载。
echo.

pause