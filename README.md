# NHSE-Mac

**适用于 macOS 的《集合啦！动物森友会》存档编辑器。**

NHSE-Mac 是一款基于 [NHSE](https://github.com/kwsch/NHSE) 开发的 macOS《集合啦！动物森友会》存档编辑器，提供简洁直观的操作界面，无需 Windows 即可管理和修改游戏存档。

## 📦 下载 | Download

请前往 [GitHub Releases](https://github.com/Augenstern1120/NHSE-Mac/releases/latest) 下载最新版本。

> ⚠️ **注意事项**
>
> 请下载 Releases 页面中的程序压缩包，而非源代码压缩包。

## 🖼️ 软件预览 | Screenshots

<table>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/c7ab53e4-ad7f-48d6-ab89-06244df4ec11" alt="NHSE-Mac Screenshot 1" width="100%">
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/dfcc9eb5-df05-49f2-bc06-5fd3b76b4586" alt="NHSE-Mac Screenshot 2" width="100%">
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/3b9b8820-e560-414f-aedf-3f6909c2fc22" alt="NHSE-Mac Screenshot 3" width="100%">
    </td>
    <td width="50%">
      <img src="https://github.com/user-attachments/assets/0bcc0d35-5c35-47b7-b59b-2fe9858d9648" alt="NHSE-Mac Screenshot 4" width="100%">
    </td>
  </tr>
</table>

## ✨ 主要功能

- **macOS 支持** — 直接在 Mac 上编辑游戏存档。
- **玩家编辑** — 查看和修改玩家信息。
- **岛屿编辑** — 管理岛屿相关数据。
- **村民管理** — 查看和编辑村民信息，包括性格、口头禅及搬离状态。
- **物品编辑** — 修改背包物品。
- **DIY 配方** — 管理已解锁的 DIY 配方。
- **存档管理** — 读取、编辑和导出存档，提供备份提醒。

## 🚀 安装与使用 | Installation & Usage

### 简体中文

1. 从 Releases 下载 `NHSE-Mac-v0.1.0-macOS-arm64.zip`。
2. 解压 ZIP 文件。
3. 双击 `start.command` 启动 NHSE-Mac。
4. 读取、编辑并导出《集合啦！动物森友会》存档。

> 📁 **存档说明：** 请选择包含 `main.dat` 和 `mainHeader.dat` 的整个存档文件夹，而不是单独选择其中某个文件。

### English

1. Download `NHSE-Mac-v0.1.0-macOS-arm64.zip` from Releases.
2. Extract the ZIP file.
3. Double-click `start.command` to launch NHSE-Mac.
4. Load, edit, and export your Animal Crossing: New Horizons save data.

> 📁 **Save Data:** Select the entire save folder containing `main.dat` and `mainHeader.dat`, rather than selecting an individual file.

## ⚠️ 启动问题 | Troubleshooting

### 简体中文

如果 macOS 阻止程序启动，请先检查 **系统设置 → 隐私与安全性**。

如果出现 `library load disallowed by system policy` 错误，可能是 macOS 阻止加载附带的 .NET 运行环境。

确认文件来源可信后，打开终端执行：

```bash
xattr -dr com.apple.quarantine "/path/to/NHSE-Mac"
```

将 `/path/to/NHSE-Mac` 替换为软件文件夹的实际路径。

### English

If macOS blocks the application, check **System Settings → Privacy & Security** first.

If you encounter `library load disallowed by system policy`, macOS may be blocking the bundled .NET runtime.

After verifying the files are trustworthy, open Terminal and run:

```bash
xattr -dr com.apple.quarantine "/path/to/NHSE-Mac"
```

Replace `/path/to/NHSE-Mac` with the actual folder path.

---

> ⚠️ **注意事项 | Note**
>
> - 修改存档前，请务必备份原始文件。
> - Always back up your original save files before editing.

## 📖 English

**A macOS-friendly save editor for Animal Crossing: New Horizons.**

NHSE-Mac is a macOS save editor based on [NHSE](https://github.com/kwsch/NHSE), featuring a graphical interface for managing and editing Animal Crossing: New Horizons save data without Windows.
