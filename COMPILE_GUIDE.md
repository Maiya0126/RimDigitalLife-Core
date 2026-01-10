# RimDigitalLife - 编译指南

**【Version: v1.1.0 | 2025-12-XX | 编译配置说明】**

## 🎯 快速编译步骤

### 1. 打开项目
- 双击 `RimDigitalLife.csproj`
- 等待 Visual Studio 加载完成

### 2. 选择配置
```
顶部工具栏：
Release  ▼ | Any CPU  ▼
```

### 3. 生成解决方案
- 按 `Ctrl + Shift + B`
- 或者点击菜单：生成 → 生成解决方案

### 4. 检查输出
成功后会在 `Assemblies/` 文件夹生成：
- `RimDigitalLife.dll`
- `RimDigitalLife.pdb`

---

## ⚙️ 环境配置（如果编译失败）

### 方法 1：设置环境变量（推荐）

1. 找到你的 RimWorld 安装目录
   - Steam: `D:\Games\Steam\steamapps\common\RimWorld\`
   - 或 GOG 版本的对应路径

2. 创建系统环境变量
   - 变量名：`RimWorldDir`
   - 变量值：`D:\Games\Steam\steamapps\common\RimWorld\`

3. 重启 Visual Studio

### 方法 2：修改项目文件

如果环境变量不工作，修改 `RimDigitalLife.csproj`：

```xml
<ItemGroup>
  <Reference Include="Assembly-CSharp">
    <HintPath>C:\Games\Steam\steamapps\common\RimWorld\RimWorld_Data\Managed\Assembly-CSharp.dll</HintPath>
    <Private>False</Private>
  </Reference>
  <Reference Include="UnityEngine">
    <HintPath>C:\Games\Steam\steamapps\common\RimWorld\RimWorld_Data\Managed\UnityEngine.dll</HintPath>
    <Private>False</Private>
  </Reference>
  <Reference Include="UnityEngine.CoreModule">
    <HintPath>C:\Games\Steam\steamapps\common\RimWorld\RimWorld_Data\Managed\UnityEngine.CoreModule.dll</HintPath>
    <Private>False</Private>
  </Reference>
</ItemGroup>
```

**把路径改成你的 RimWorld 安装目录！**

---

## 📦 编译后的文件结构

```
RimDigitalLife/
├── Assemblies/                    ← 生成的 DLL 在这里
│   ├── RimDigitalLife.dll        ✅ 主要文件
│   └── RimDigitalLife.pdb        (调试符号)
├── About/
│   └── About.xml
├── Defs/
│   └── (所有 XML 文件)
├── Source/
│   └── (所有 C# 源文件)
└── (其他文件...)

需要复制到 Mods 的文件：
✅ 整个 RimDigitalLife 文件夹
```

---

## 🚀 部署到游戏

### 步骤 1：复制 Mod 文件夹
```
从：
D:\Visual Studio Code ALL\RimDigitalLife\

复制到：
D:\Games\Steam\steamapps\common\RimWorld\Mods\RimDigitalLife\
```

### 步骤 2：启动游戏
1. 启动 RimWorld
2. 点击 "Mod"
3. 在列表中找到 "RimDigital: Core"
4. 勾选它
5. 点击 "加载保存" 或 "新游戏"

### 步骤 3：检查加载
打开开发者控制台（按 `~` 或 `§`），输入：
```
 RimDigital: Core
```
应该看到初始化日志：
```
[RimDigital: Core] v1.1.0 Initialized | Build: 202512XX | Author: Maiya0126
```

---

## ❓ 常见问题

### Q1: 编译时提示 "找不到 Harmony"
**A:** 确保已安装 NuGet 包：
- 右键项目 → 管理 NuGet 程序包
- 搜索 "Lib.Harmony"
- 安装版本 2.2.2

### Q2: 编译成功但游戏不识别 Mod
**A:** 检查 About.xml 路径：
- 必须在 RimDigitalLife/About/About.xml
- 不能是 RimDigitalLife/About.xml

### Q3: 游戏加载时红字报错
**A:** 发送截图给我，我会分析错误

---

**作者：Maiya0126 (麦丫)**
**Signature: 6Zt4562w5p2lTWFpeWEwMTI2**
