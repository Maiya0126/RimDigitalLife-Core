@echo off
REM ============================================================================
REM RimDigitalLife - 手动编译脚本（不使用 Visual Studio）
REM 【Version: v1.1.0 | 修正路径版】
REM @author Maiya0126 (麦丫)
REM ============================================================================

echo.
echo ============================================================
echo  RimDigitalLife - 离线编译工具
echo  版本: v1.1.0
echo  作者: Maiya0126 (麦丫)
echo ============================================================
echo.

REM 设置 RimWorld 路径（修正为 RimWorldWin64_Data）
set RIMWORLD_DIR=D:\Games\steamapps\common\RimWorld

REM 设置输出目录
set OUTPUT_DIR=%~dp0Assemblies

echo [信息] RimWorld 路径: %RIMWORLD_DIR%
echo [信息] 输出目录: %OUTPUT_DIR%
echo [信息] Harmony 路径: D:\games\steamapps\workshop\content\294100\2009463077\Current\Assemblies
echo.

REM 检查 C# 编译器
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo [错误] 找不到 C# 编译器
    echo.
    echo 正在尝试查找其他版本...

    REM 尝试其他可能的位置
    for /d %%i in (C:\Windows\Microsoft.NET\Framework64\v4*) do (
        if exist "%%i\csc.exe" (
            set CSC=%%i\csc.exe
            echo [信息] 找到编译器: %%i\csc.exe
            goto :found_compiler
        )
    )

    echo [错误] 无法找到 C# 编译器
    echo.
    echo 请安装 .NET Framework 4.7.2 开发工具包
    pause
    exit /b 1
)

:found_compiler
echo [信息] 使用编译器: %CSC%
echo.

REM 创建输出目录
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo [信息] 开始编译...
echo.

REM 编译所有 C# 文件（递归 Source 文件夹）
"%CSC%"^
 /target:library^
 /out:"%OUTPUT_DIR%\RimDigitalLife.dll"^
 /reference:"%RIMWORLD_DIR%\RimWorldWin64_Data\Managed\Assembly-CSharp.dll"^
 /reference:"%RIMWORLD_DIR%\RimWorldWin64_Data\Managed\UnityEngine.dll"^
 /reference:"%RIMWORLD_DIR%\RimWorldWin64_Data\Managed\UnityEngine.CoreModule.dll"^
 /reference:"D:\games\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll"^
 /reference:System.dll^
 /reference:System.Core.dll^
 /reference:System.Xml.dll^
 /reference:System.Xml.Linq.dll^
 /nologo^
 /optimize^
 /unsafe^
 /langversion:latest^
 /recurse:Source\*.cs

if %errorlevel% equ 0 (
    echo.
    echo ============================================================
    echo [成功] 编译完成！
    echo ============================================================
    echo.
    echo 输出文件:
    dir "%OUTPUT_DIR%\RimDigitalLife.dll"
    echo.
    echo 下一步:
    echo   1. 复制整个 RimDigitalLife 文件夹到 RimWorld\Mods\
    echo   2. 启动游戏测试
    echo.
) else (
    echo.
    echo ============================================================
    echo [失败] 编译失败，请检查错误信息
    echo ============================================================
    echo.
    echo 常见问题:
    echo   - 检查 RimWorld 路径是否正确
    echo   - 检查 Harmony DLL 是否存在
    echo   - 检查 Source\ 文件夹是否存在
    echo.
)

pause
