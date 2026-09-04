# EyeCare20 · 护眼提醒小工具

> 每用眼 20 分钟，望远 20 秒 —— 一个极小、零依赖、绿色单文件的 Windows 桌面护眼助手

[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)]()
[![Size](https://img.shields.io/badge/size-极小-brightgreen)]()
[![Runtime](https://img.shields.io/badge/runtime-.NET%20Framework%204.8%20%E9%A2%84%E8%A3%85-success)]()
[![License](https://img.shields.io/badge/license-MIT-green)]()

**[中文](#中文)** · **[English](#english)** · **[专题页](https://gitee.com/songyun/EyeCare20)**

---

## 中文

### 为什么是 EyeCare20

长时间盯屏幕带来的眼干、眼疲劳、久坐和缺水，靠意志力提醒自己没用——让软件替你记得。
基于医学推荐的 **20-20-20 法则**（每 20 分钟，看 20 英尺≈6 米外，持续 20 秒）与眨眼训练设计。

它有多小？**一个不到 100KB 的单文件 exe**，不做任何事时仅占用约 35MB 内存，
不联网、无任何依赖——Windows 10/11 预装运行时，下载即用。

### 功能特性

- **四类健康提醒统一管理**：望远休息（20-20-20）、眨眼训练（闭眼 2 秒 + 完整眨眼 5 次）、久坐提醒、喝水提醒——每类可独立开关/设间隔

- **智能合并与错峰**：到期时间相差 5 分钟内的提醒自动合并为一张卡片同时弹出；重排后任意两个提醒至少相差 5 分钟，减少打扰

- **休息中智能暂停**：休息倒计时期间检测到键鼠操作或全屏应用（游戏/电影）→ 倒计时自动冻结，真正停下来才继续；看视频/听歌不受影响

- **双模式计时**：简单模式按系统时间循环；高级模式仅在操作电脑（键鼠/音频输出）时累计时长

- **数据统计**：按日记录完成/跳过/休息时长，今日摘要 + 最近 7 天柱状图，本地存储

- **自动更新**：内置 Gitee/GitHub 双源回退（国内 Gitee 优先，国外 GitHub 回退），一键完成下载→替换→重启

- **清爽界面**：屏幕居中提醒卡、环形倒计时、矢量自绘图标（零图片资源）、主色 `#048A4A` 极简风格

- **细节体验**：开机自启（免管理员）、全屏免打扰、锁屏不停歇、单实例、不抢键盘焦点

- **多语言**：根据系统语言自动切换中英文，也可在设置中手动切换

### 截图

|                  望远休息提醒卡                  |             主界面（剩余时间）             |
| :---------------------------------------: | :-------------------------------: |
| ![望远休息提醒卡](screenshots/reminder-card.png) | ![主界面](screenshots/main-form.png) |

### 下载安装

1. 从任一平台下载 `EyeCare20.exe`（单文件，无需安装）：

   - **Gitee**：[Releases 下载](https://gitee.com/songyun/EyeCare20/releases)

   - **GitHub**：[Releases 下载](https://github.com/chenbf0713/EyeCare20/releases)
2. 双击运行即可（首次运行若被 SmartScreen 提示：右键 exe → 属性 → 勾选"解除锁定"）
3. 托盘右键 → **开机自启动**，一次设置长期有效

> 配置与统计数据保存在 `%APPDATA%\EyeCare20\`，本地存储，升级或换机不会丢失。

### 使用说明

- **双击托盘图标**：打开主界面，实时查看各提醒的剩余倒计时

- **右键托盘图标**：主界面 / 设置 / 统计 / 检查更新 / 立即望远休息 / 暂停 1 小时 / 退出

- **设置页**：切换计时模式、调整四类提醒的间隔与开关、提醒声音、语言、自启动

- **更新**：启动时自动静默检查；发现新版本弹窗，点"立即更新"即自动完成（下载→替换→重启约 2 秒）

### 更新源说明（重要）

软件内置双更新源，开箱即用：

| 优先级 | 平台           | update.json 地址                                                            |
| --- | ------------ | ------------------------------------------------------------------------- |
| 1   | Gitee（国内快）   | `https://gitee.com/songyun/EyeCare20/raw/main/update.json`                |
| 2   | GitHub（国际回退） | `https://raw.githubusercontent.com/chenbf0713/EyeCare20/main/update.json` |

自建更新源：在 `%APPDATA%\EyeCare20\config.json` 的 `UpdateUrl` 填写地址即可覆盖（多个地址用 `|` 分隔，按序回退）。
`update.json` 格式：

```json
{
  "version": "1.3.1.0",
  "downloadUrl": "https://gitee.com/songyun/EyeCare20/releases/download/v1.3.1/EyeCare20.exe",
  "downloadUrlAlt": "https://github.com/chenbf0713/EyeCare20/releases/download/v1.3.1/EyeCare20.exe",
  "notes": "更新说明"
}
```

`downloadUrl`（主，建议 Gitee Releases）失败时自动尝试 `downloadUrlAlt`（备用，建议 GitHub Releases）。

### 从源码构建

```bash
git clone https://gitee.com/songyun/EyeCare20.git   # 或 github
cd EyeCare20
dotnet build -c Release
# 产物：bin/Release/net48/EyeCare20.exe
```

无 SDK 环境兜底（Windows 自带编译器，仅支持 C# 5 语法，本项目已按此约束编写）：

```bash
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:EyeCare20.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll ^
  /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll *.cs
```

### 科学依据

- [20-20-20 法则](https://lookaway.com/20-20-20-rule/)：美国眼科学会推荐的数字眼疲劳缓解方式，Aston 大学 2022 年研究曾对其进行严格验证（[研究摘要](https://pubmed.ncbi.nlm.nih.gov/35963776/)）

- 眨眼训练：屏幕专注时眨眼频率会明显下降，刻意完整眨眼是干眼管理的常见行为疗法

- 久坐与定时补水：日常健康习惯的常见建议

### 路线图

**核心提醒功能免费**，无功能墙、无提醒次数限制。当前已实现：

- 📊 **今日报告**：当日完成/跳过/休息时长摘要 + 最近 7 天柱状图

### 请作者喝杯咖啡

EyeCare20 免费开源。如果它帮到了你，欢迎请作者喝杯咖啡 ☕：

- **爱发电**：[afdian.com/your-id](https://afdian.com/your-id)（推荐，支持一次性与月度）

- **微信 / 支付宝**：收款码见下方（放入 `donate/` 目录后自动显示）

|              微信             |              支付宝             |
| :-------------------------: | :--------------------------: |
| ![微信收款码](donate/wechat.png) | ![支付宝收款码](donate/alipay.png) |

> 扫码即可支持作者——也欢迎 Star 代替咖啡 ✨

### 许可证

[MIT](LICENSE) — 自由使用、修改、分发。

> 提示：这是健康辅助工具，不能替代医疗建议。持续眼不适请就医。

### 常见问题

**Q：护眼软件哪个好？EyeCare20 适合什么人？**
适合长时间使用电脑的办公族、程序员、学生，需要定时望远休息、眨眼训练、久坐和喝水提醒的 Windows 用户。软件体积小、零依赖，适合追求轻量绿色工具的用户。

**Q：20-20-20 法则是什么？**
每使用屏幕 20 分钟，看向 20 英尺（约 6 米）外的物体 20 秒，是缓解数字眼疲劳的常见建议。

**Q：EyeCare20 联网吗？会上传用户数据吗？**
软件本身不联网、不上传数据，所有配置和统计本地存储在 `%APPDATA%\EyeCare20\`。仅在检查更新时访问 Gitee/GitHub 的 update.json 与发行版附件。

**Q：护眼提醒、久坐提醒、喝水提醒可以同时开吗？**
可以。四类提醒可独立开关与设间隔，智能合并与错峰机制避免同时打扰。

**Q：Windows 没有安装 .NET Framework 能用吗？**
Windows 10/11 已预装 .NET Framework 4.8，下载即用。更旧的 Windows 7/8 需手动安装运行时。

### 关键词

护眼软件 · 眼疲劳提醒 · 20-20-20 法则 · 久坐提醒 · 喝水提醒 · 眨眼训练 · 干眼症预防 · Windows 桌面提醒工具 · 绿色单文件 · 开源护眼软件 · 数字眼疲劳 · 视力保护

---

## English

**EyeCare20** is a tiny, zero-dependency Windows tray app for the 20-20-20 eye-care rule, plus blink training, sitting and hydration reminders — with smart merge/spacing between reminders, countdown auto-pause on input or fullscreen apps, daily stats, dual-source (Gitee/GitHub) self-updating, and multilingual support (Chinese/English, auto-detected from system language). Built with plain C# WinForms (.NET Framework 4.8 preinstalled on Windows 10/11).

### Why EyeCare20

Staring at screens causes eye strain, dry eyes, sedentary fatigue, and dehydration — willpower alone can't remind you to take breaks. EyeCare20 is based on the medically recommended **20-20-20 rule** (every 20 minutes, look at something 20 feet ≈ 6 m away for 20 seconds) and blink training.

How tiny is it? **A single exe under 100KB**, using ~35MB memory when idle, no network, no dependencies — Windows 10/11 has the runtime preinstalled, just download and run.

### Features

- **Four health reminders in one**: Look-away (20-20-20), blink training (close eyes 2s + full blinks 5x), sit break, hydration — each independently toggleable with custom intervals

- **Smart merge & spacing**: Reminders due within 5 minutes of each other merge into one card; any two reminders are spaced at least 5 minutes apart, reducing interruptions

- **Smart pause during rest**: Countdown freezes when keyboard/mouse activity or fullscreen apps (games/movies) are detected; watching videos or listening to music is unaffected

- **Dual timing modes**: Simple mode cycles by system time; Advanced mode counts only active computer use (keyboard/mouse/audio output)

- **Daily stats**: Records completed/skipped/rest duration per day, today's summary + 7-day bar chart, stored locally

- **Auto update**: Built-in Gitee/GitHub dual-source fallback, one-click download → replace → restart

- **Clean UI**: Centered reminder card, ring countdown, vector-drawn icons (zero image resources), `#048A4A` minimalist theme

- **Details**: Autostart (no admin needed), fullscreen DND, lock-screen resilient, single instance, no keyboard focus stealing

- **Multilingual**: Auto-switches Chinese/English based on system language, manual override in settings

### Screenshots

|                  Look-away reminder card                  |             Main window (remaining time)             |
| :----------------------------------------------: | :--------------------------------------: |
| ![Look-away reminder card](screenshots/reminder-card.png) | ![Main window](screenshots/main-form.png) |

### Download & Install

1. Download `EyeCare20.exe` from either platform (single file, no installation needed):

   - **Gitee**: [Releases](https://gitee.com/songyun/EyeCare20/releases)

   - **GitHub**: [Releases](https://github.com/chenbf0713/EyeCare20/releases)
2. Double-click to run (if SmartScreen warns: right-click exe → Properties → check "Unblock")
3. Right-click tray icon → **Auto-start** — set once, works long-term

> Config and stats are stored locally in `%APPDATA%\EyeCare20\`, surviving upgrades and machine migration.

### Usage

- **Double-click tray icon**: Open main window, view remaining countdown for each reminder

- **Right-click tray icon**: Main window / Settings / Stats / Check for updates / Rest now / Pause 1 hour / Exit

- **Settings**: Switch timing mode, adjust intervals and toggles for all four reminders, sound, language, autostart

- **Updates**: Auto-check silently on startup; pop up when new version found, click "Update now" to auto-complete (download → replace → restart, ~2 seconds)

### Update Sources

The app has built-in dual update sources, working out of the box:

| Priority | Platform | update.json URL |
| --- | -------- | --------------- |
| 1 | Gitee (fast in China) | `https://gitee.com/songyun/EyeCare20/raw/main/update.json` |
| 2 | GitHub (international fallback) | `https://raw.githubusercontent.com/chenbf0713/EyeCare20/main/update.json` |

Custom update source: set `UpdateUrl` in `%APPDATA%\EyeCare20\config.json` (multiple URLs separated by `|`, tried in order).
`update.json` format:

```json
{
  "version": "1.3.1.0",
  "downloadUrl": "https://gitee.com/songyun/EyeCare20/releases/download/v1.3.1/EyeCare20.exe",
  "downloadUrlAlt": "https://github.com/chenbf0713/EyeCare20/releases/download/v1.3.1/EyeCare20.exe",
  "notes": "Release notes"
}
```

### Build from Source

```bash
git clone https://github.com/chenbf0713/EyeCare20.git   # or gitee
cd EyeCare20
dotnet build -c Release
# Output: bin/Release/net48/EyeCare20.exe
```

Fallback without SDK (Windows built-in compiler, C# 5 syntax only):

```bash
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:EyeCare20.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll ^
  /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll *.cs
```

### Scientific Basis

- [20-20-20 rule](https://lookaway.com/20-20-20-rule/): A digital eye strain relief method recommended by the American Academy of Ophthalmology. Aston University's 2022 study rigorously validated it ([study abstract](https://pubmed.ncbi.nlm.nih.gov/35963776/))

- Blink training: Blink frequency drops significantly during screen focus; deliberate full blinks are a common behavioral therapy for dry eye management

- Sedentary breaks & hydration: Common daily health habit recommendations

### Roadmap

**Core reminder features are free**, no paywalls, no reminder count limits. Currently implemented:

- 📊 **Today's report**: Daily completed/skipped/rest duration summary + 7-day bar chart

### Buy the Author a Coffee

EyeCare20 is free and open source. If it helped you, consider buying the author a coffee ☕:

- **WeChat / Alipay**: QR codes below

|              WeChat              |              Alipay              |
| :------------------------------: | :------------------------------: |
| ![WeChat QR](donate/wechat.png)  | ![Alipay QR](donate/alipay.png)  |

> Scan to support the author — or just Star the repo ✨

### License

[MIT](LICENSE) — free to use, modify, and distribute.

> Note: This is a health aid tool, not a substitute for medical advice. See a doctor for persistent eye discomfort.

### FAQ

**Q: Who is EyeCare20 for?**
Office workers, programmers, and students who spend long hours on computers and need timed look-away, blink, sitting, and hydration reminders on Windows. Small size, zero dependencies — ideal for users who want lightweight portable tools.

**Q: What is the 20-20-20 rule?**
Every 20 minutes of screen use, look at an object 20 feet (about 6 meters) away for 20 seconds. It's a common recommendation for reducing digital eye strain.

**Q: Does EyeCare20 connect to the internet or upload user data?**
The app itself doesn't connect or upload data. All config and stats are stored locally in `%APPDATA%\EyeCare20\`. It only accesses Gitee/GitHub for update checking.

**Q: Can eye, sitting, and hydration reminders run simultaneously?**
Yes. All four reminders can be toggled and configured independently. Smart merge and spacing prevents simultaneous interruptions.

**Q: Can I use it without .NET Framework installed?**
Windows 10/11 has .NET Framework 4.8 preinstalled, so it works out of the box. Older Windows 7/8 may need to install the runtime manually.

### Keywords

eye care software, eye strain reminder, 20-20-20 rule, sit break reminder, hydration reminder, blink training, dry eye prevention, Windows desktop reminder tool, portable single file, open source eye care, digital eye strain, vision protection
