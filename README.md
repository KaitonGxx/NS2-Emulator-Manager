# NS2 Emulator Manager

Install, update and launch Switch 2 emulators on Windows, from one small window.

Made by **Riskbreak** (Discord).

![The main window with three emulators installed](docs/main.png)

## What it does

- **Installs** Eglantine Main, PokeDinner and Lichii, or any other fork from a GitHub or Gitea link.
- **Updates** with one click. The version you replace is kept, so you can go back.
- **Choose any version** from a list of every release (each card's "..." menu).
- **Keeps your saves safe.** Each emulator uses its own folder; nothing goes in `%APPDATA%`.
- **Share data with other emulators:** tick it on one emulator and all of them use its keys, firmware and saves, so
  your progress follows you. Saves for different games are combined; nothing is deleted. When you stop, choose
  whether the others keep the latest saves or restore their own.
- **Save Helper** shows where every emulator's saves are, the backups, and how to back them up.
- **Finds emulators you already have**, so nothing is downloaded twice.
- **Updates itself** from this page when a new version is out.

## Screenshots

**First start:** tick the emulators you want, then click Install.

![Welcome window](docs/welcome.png)

**Already have them?** Emulators that are already in the folder are added without downloading them again.

![Welcome window with emulators already installed](docs/welcome-already-installed.png)

**Choose any version** from each emulator's "..." menu.

![Choose version list](docs/choose-version.png)

**Pick your colours** from 16 themes with the Themes button.

![Themes window](docs/themes.png)

**No emulators yet:** one click to pick some.

![Main window with no emulators](docs/no-emulators.png)

## Install

![Setup](docs/setup.png)

1. Go to [Releases](../../releases) and download the latest `NS2EmulatorManager.zip`.
2. Extract the whole zip (right-click it, then **Extract All**).
3. Run **Setup.exe** from the extracted folder.

It installs for your user only (no admin needed). Uninstall from **Settings > Apps**.

### Portable version (no install)

Want to keep everything on a USB stick, a games drive, or in one folder you can copy? Download
`NS2EmulatorManager-Portable.zip` instead, extract it where you want it, and open **EmuManager.exe**.

- **Nothing is added to Windows:** no Apps & features entry, no shortcuts.
- **Everything stays in that folder:** the manager's settings, your emulators and their saves. Copy the whole folder to
  back it up or take it to another PC. It finds its way again even if the drive letter changes.
- **Updates itself** like the installed version, and still adds nothing to Windows.
- **Removed things** go to the folder's own `Removed items` folder (USB sticks have no Recycle Bin).
- **USB sticks formatted as FAT32 or exFAT** (most of them) can't share saves between emulators; each one keeps its
  own. NTFS drives can.
- Keep `portable.txt` next to `EmuManager.exe`: it's what makes it portable.

## Keys and firmware

The emulators need keys and firmware **from your own Switch**. This tool doesn't download or include them.
Each emulator's card shows simple steps to add them.

## Build from source

Needs Windows and Python 3 (it uses the C# compiler that comes with Windows):

```
python manager.py build        # builds EmuManager.exe
python manager.py installer    # builds dist\NS2EmulatorManager.zip (Setup.exe + EmuManager.exe)
```

The icon is drawn from `src\icon.html`. To redraw it, run `python src\make_icon.py`, which needs Microsoft Edge and Pillow.

## Credits

- The emulators are made and hosted by their own projects:
  [Eglantine](https://github.com/Eglantine-Community/Eglantine),
  [PokeDinner](https://git.eden-emu.dev/Franseven/PokeDinner-a-Eglantine-Fork),
  [Lichii](https://github.com/zortos293/Lichii).
  This tool only downloads their official releases.
- The emulator logos on the cards belong to their projects: Eglantine's flower (PokeDinner's badge adds a ball to it)
  and Lichii's lychee.

## License

GPL-3.0. See [LICENSE](LICENSE).
