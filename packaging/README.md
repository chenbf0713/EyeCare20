# Microsoft Store 上架步骤（MSIX）

## 一次性准备

1. **注册开发者账户**：https://partner.microsoft.com/dashboard/registration
   - 个人账户一次性 ¥141 左右（$19）
2. 在合作伙伴中心 **创建新应用**，保留名称（如 `EyeCare20 护眼提醒`）
3. 记下分配的 **Package/Identity**（提交时会自动注入，无需手填 manifest）

## 打包流程

```powershell
# 1. 准备资产图（Store 有在线生成器）：
#    assets/StoreLogo.png (300x300), Square150x150Logo.png, Square44x44Logo.png,
#    Wide310x150Logo.png, Square310x310Logo.png —— 用项目里 EyeCare20.ico 的绿眼睛图形导出各尺寸

# 2. 建包目录（本项目为桌面 Win32 → MSIX 打包）：
mkdir package
copy ..\bin\Release\net48\EyeCare20.exe package\
copy assets package\assets\

# 3. 生成 AppxManifest.xml（用本目录模板，Identity 由商店提交时替换）
#    方式 A（推荐，带签名校验）：使用 MakeAppx +商店 "MSIX 打包工具"
MakeAppx.exe pack /d package /p EyeCare20.msix

# 4. 提交到合作伙伴中心：
#    应用概览 → 开始提交 → 包 → 上传 .msix（商店会自动签名）
```

## 更省事的替代路径

不装 WDK/SDK 时，直接用商店官方工具 **Windows App Certification Kit + "MSIX Packaging Tool"**（Microsoft Store 免费下载）：
安装 → 选 "App package for single EXE" → 指向 `bin\Release\net48\EyeCare20.exe` → 按向导生成即可。

## 提交注意（容易被打回的点）

- ✅ 本程序无需任何受限能力（manifest 已用 `runFullTrust`，标准桌面打包）
- ✅ 无网络上报、无广告、无采集 —— 隐私声明可写"不收集任何数据"（StatsStore 全本地）
- ✅ 应用描述别写"治疗/预防近视"等医疗功效词，用"提醒/缓解疲劳表述"（健康类目审查）
- ✅ 至少 1 张截图（用 screenshots/ 里的图）+ 一句简介 + 隐私政策 URL（可指向专题页 `docs/index.html` 部署后的地址）

## 定价建议

- 定价 **¥12.9**（一次性买断）
- 商店版与开源版同源：开源仓库保持免费（口碑池），商店收费（付费池）
- 可在商店描述里注明"开源地址"，避免被投诉双标
