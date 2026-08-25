@echo off
REM ============================================================================
REM RimDigitalLife - 编译脚本 (v2.0 | MSBuild 版)
REM @author Maiya0126 (麦丫)
REM 说明: 使用 Visual Studio 项目编译 Release，产出到 RimDigitalLife\bin\Release
REM       并触发 csproj 的 PostBuildEvent 自动部署到游戏 Mods 文件夹
REM ============================================================================

echo.
echo ============================================================
echo  RimDigitalLife - 编译工具 (MSBuild)
echo ============================================================
echo.

REM 使用 VS2022 MSBuild
set MSBUILD="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
set CSPROJ=D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife\RimDigitalLife.csproj

if not exist %MSBUILD% (
    echo [错误] 找不到 VS2022 MSBuild，请确认安装了 Visual Studio 2022
    pause
    exit /b 1
)

if not exist "%CSPROJ%" (
    echo [错误] 找不到项目文件: %CSPROJ%
    pause
    exit /b 1
)

echo [信息] MSBuild: %MSBUILD%
echo [信息] 项目: %CSPROJ%
echo [信息] 开始编译 Release ...
echo.

%MSBUILD% "%CSPROJ%" /p:Configuration=Release /t:Build /v:minimal

if %errorlevel% equ 0 (
    echo.
    echo ============================================================
    echo [成功] 编译并部署完成！
    echo ============================================================
    echo.
    echo 输出 DLL: D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife\bin\Release\RimDigitalLife.dll
    echo 已自动复制到游戏 Mods 文件夹。
    echo.
) else (
    echo.
    echo ============================================================
    echo [失败] 编译失败，请检查上方错误信息
    echo ============================================================
    echo.
)

pause