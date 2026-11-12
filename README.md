# 🎮 NS2 Emulator Manager

[![Latest release](https://img.shields.io/github/v/release/KaitonGxx/NS2-Emulator-Manager?label=release)](https://github.com/KaitonGxx/NS2-Emulator-Manager/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/KaitonGxx/NS2-Emulator-Manager/total?label=downloads)](https://github.com/KaitonGxx/NS2-Emulator-Manager/releases)
[![License: GPL-3.0](https://img.shields.io/github/license/KaitonGxx/NS2-Emulator-Manager)](LICENSE)
[![Windows 10/11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows&logoColor=white)](#-install)

Install, update and launch Switch 2 emulators on Windows, from one small window.

Tick the emulators you want and click Install. From then on, every update is one click, your saves stay where they
are, and you can go back to any older version whenever you like. It handles Eglantine Main, PokeDinner and Lichii out
of the box, and any other fork from a GitHub or Gitea link.

Made by **Riskbreak** (Discord).

> **AI disclosure:** NS2 Emulator Manager was written with Claude, Anthropic's AI assistant, under the direction of its
> maintainer, who tested the changes on Windows.

![The main window with three emulators installed](docs/main.png)

## ✨ Features

**📥 Install and update**
- **Installs** Eglantine Main, PokeDinner and Lichii, or any other fork from a GitHub or Gitea link.
- **Updates** with one click. The version you replace is kept, so you can go back.
- **Choose any version** from a list of every release (each card's "..." menu).
- **Finds emulators you already have**, so nothing is downloaded twice.
- **Updates itself** from this page when a new version is out.

**💾 Your saves, kept safe**
- **Each emulator uses its own folder.** Nothing goes in `%APPDATA%`.
- **Share data with other emulators:** tick it on one emulator and all of them use its keys, firmware and saves, so
  your progress follows you. Saves for different games are combined, and nothing is deleted. When you stop, choose
  whether the others keep the latest saves or restore their own.
- **Save Helper** shows where every emulator's saves are, the backups, and how to back them up.

**🎨 Make it yours**
- **16 themes** from the Themes button at the top.
- **Each emulator's own logo** on its card.

**🎒 Portable if you want it**
- A **no-install version** that keeps everything in one folder, for a USB stick, a games drive or a backup you can
  copy. See [Portable version](#-portable-version-no-install).

## 📸 Screenshots

| First start | Already have them? |
|:---:|:---:|
| <img src="docs/welcome.png" alt="Welcome window: tick the emulators you want, then click Install"> | <img src="docs/welcome-already-installed.png" alt="Emulators already in the folder are added without downloading them again"> |
| **Choose any version** | **16 themes** |
| <img src="docs/choose-version.png" alt="The list of every release, from each card's ... menu"> | <img src="docs/themes.png" alt="The Themes window"> |
| **No emulators yet** | **Setup** |
| <img src="docs/no-emulators.png" alt="The main window with no emulators: one click to pick some"> | <img src="docs/setup.png" alt="The setup window"> |

## 📲 Install

1. Go to [Releases](https://github.com/KaitonGxx/NS2-Emulator-Manager/releases/latest) and download the latest
   `NS2EmulatorManager.zip`.
2. Extract the whole zip (right-click it, then **Extract All**).
3. Run **Setup.exe** from the extracted folder.

It installs for your user only (no admin needed). Uninstall from **Settings > Apps**.

> [!TIP]
> **"Windows protected your PC"?** Click **More info**, then **Run anyway**. Windows shows this for new programs that
> aren't widely downloaded yet.

### 🎒 Portable version (no install)

Want to keep everything on a USB stick, a games drive, or in one folder you can copy? Download
`NS2EmulatorManager-Portable.zip` instead, extract it where you want it, and open **EmuManager.exe**.

- **Nothing is added to Windows:** no Apps & features entry, no shortcuts.
- **Everything stays in that folder:** the manager's settings, your emulators and their saves. Copy the whole folder to
  back it up or take it to another PC. It finds its way again even if the drive letter changes.
- **Updates itself** like the installed version, and still adds nothing to Windows.
- **Removed things** go to the folder's own `Removed items` folder (USB sticks have no Recycle Bin).

> [!NOTE]
> **USB sticks formatted as FAT32 or exFAT** (most of them) can't share saves between emulators, so each one keeps its
> own. NTFS drives can.

> [!IMPORTANT]
> Keep `portable.txt` next to `EmuManager.exe`. It's what makes it portable.

## 🔑 Keys and firmware

> [!IMPORTANT]
> The emulators need keys and firmware **from your own Switch**. This tool doesn't download or include them.

Each emulator's card shows simple steps to add them.

## 🛠️ Build from source

Needs Windows and Python 3 (it uses the C# compiler that comes with Windows):

```
python manager.py build        # builds EmuManager.exe
python manager.py installer    # builds dist\NS2EmulatorManager.zip and dist\NS2EmulatorManager-Portable.zip
```

The icon is drawn from `src\icon.html`. To redraw it, run `python src\make_icon.py`, which needs Microsoft Edge and Pillow.

## 🙏 Credits

- The emulators are made and hosted by their own projects:
  [Eglantine](https://github.com/Eglantine-Community/Eglantine),
  [PokeDinner](https://git.eden-emu.dev/Franseven/PokeDinner-a-Eglantine-Fork),
  [Lichii](https://github.com/zortos293/Lichii).
  This tool only downloads their official releases.
- The emulator logos on the cards belong to their projects: Eglantine's flower (PokeDinner's badge adds a ball to it)
  and Lichii's lychee.

## 📜 License

NS2 Emulator Manager is free software, released under the [GNU General Public License v3.0](LICENSE).

It isn't affiliated with or endorsed by Nintendo or any of the emulator projects above.
