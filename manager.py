"""NS2 Emulator Manager build helper (Windows; needs only the .NET Framework 4 compiler that ships with Windows).

  python manager.py build       # compile src\\*.cs to EmuManager.next.exe, then swap it in if the app isn't running
  python manager.py shortcut    # (re)make the desktop shortcuts "Switch 2 Emulators" and "Switch 2 Emulators (Test)"
                                # (Test = EmuManager.exe --sandbox .\\sandbox: a new user's first start; Reset test wipes it)
"""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, 'src')
EXE = os.path.join(HERE, 'EmuManager.exe')
ICO = os.path.join(SRC, 'app.ico')
CSC = os.path.join(os.environ.get('WINDIR', r'C:\Windows'), r'Microsoft.NET\Framework64\v4.0.30319\csc.exe')
LNK = os.path.join(os.path.expanduser('~'), 'Desktop', 'Switch 2 Emulators.lnk')
PNG = os.path.join(SRC, 'app.png')
SANDBOX = os.path.join(HERE, 'sandbox')
LNK_TEST = os.path.join(os.path.expanduser('~'), 'Desktop', 'Switch 2 Emulators (Test).lnk')
# The icon is drawn from src\icon.html by src\make_icon.py (with Edge). Delete src\app.ico and src\app.png to draw
# them again on the next build; only that needs Microsoft Edge and Pillow, a normal build doesn't.


def ps(script):
    return subprocess.run(['powershell', '-NoProfile', '-Command', script], capture_output=True, text=True)


def running():
    out = subprocess.run(['tasklist', '/FI', 'IMAGENAME eq EmuManager.exe', '/NH'], capture_output=True, text=True).stdout
    return 'emumanager.exe' in out.lower()


def cmd_build(_):
    if not (os.path.exists(ICO) and os.path.exists(PNG)):
        r = subprocess.run([sys.executable, os.path.join(SRC, 'make_icon.py')], capture_output=True, text=True)
        print(r.stdout.strip() or r.stderr.strip())
        if not (os.path.exists(ICO) and os.path.exists(PNG)):
            return 1
    out = os.path.join(HERE, 'EmuManager.next.exe')
    args = [CSC, '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/out:' + out,
            '/r:System.Web.Extensions.dll', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Core.dll', '/r:Microsoft.VisualBasic.dll',
            '/win32icon:' + ICO, '/resource:' + PNG + ',app.png', '/resource:' + ICO + ',app.ico']
    # the built-in emulators' own logos for their cards (src\badges\<id>.png -> resource badge-<id>.png)
    for f in sorted(os.listdir(os.path.join(SRC, 'badges'))):
        if f.lower().endswith('.png'):
            args.append('/resource:' + os.path.join(SRC, 'badges', f) + ',badge-' + f.lower())
    args += ['/win32manifest:app.manifest', 'AppInfo.cs', 'FolderPicker.cs', 'EmuCore.cs', 'UserData.cs', 'EmuForm.cs']   # no setup code in the app
    r = subprocess.run(args, cwd=SRC, capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stdout + r.stderr)
        return 1
    print('built', out, r.stdout.strip() or 'no warnings')
    if _ == 'no-swap' or (isinstance(_, list) and 'no-swap' in _):
        return 0
    if running():
        print('EmuManager.exe is running; close it and run build again to swap in the new exe')
        return 0
    os.replace(out, EXE)
    print('swapped in', EXE)
    return 0


def make_lnk(path, args, desc):
    r = ps("$s=(New-Object -ComObject WScript.Shell).CreateShortcut('%s'); $s.TargetPath='%s'; $s.Arguments='%s'; $s.WorkingDirectory='%s';"
           "$s.Description='%s'; $s.IconLocation='%s,0'; $s.Save()"
           % (path, EXE, args.replace("'", "''"), HERE, desc, EXE))
    print(r.stdout + r.stderr if r.returncode else 'shortcut: ' + path)
    return r.returncode


def cmd_shortcut(_):
    """Both desktop shortcuts: the manager, and its test environment (a new user's first start, in .\\sandbox)."""
    rc = make_lnk(LNK, '', 'Install and update the Switch 2 emulator builds')
    rc |= make_lnk(LNK_TEST, '--sandbox "%s"' % SANDBOX, 'Emulator Manager test environment: starts like a new user, only uses its own folder')
    return rc


def cmd_installer(_):
    """dist\\NS2EmulatorManager.zip = Setup.exe (src\\Setup.cs, its own small program) + EmuManager.exe (the app),
    side by side, like a normal installer's files. Setup copies EmuManager.exe in. No exe is embedded in another
    (antivirus models call that a dropper) and the app never installs itself (self-copy + launchers + downloads reads
    as a trojan). The unzipped folder is kept in dist\\NS2EmulatorManager for testing."""
    if cmd_build('no-swap'):
        return 1
    import shutil, zipfile
    stage = os.path.join(HERE, 'dist', 'NS2EmulatorManager')
    if os.path.isdir(stage):
        shutil.rmtree(stage)
    os.makedirs(stage)
    shutil.copyfile(os.path.join(HERE, 'EmuManager.next.exe'), os.path.join(stage, 'EmuManager.exe'))
    args = [CSC, '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/out:' + os.path.join(stage, 'Setup.exe'), '/main:SetupApp',
            '/r:System.Web.Extensions.dll', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Core.dll', '/r:Microsoft.VisualBasic.dll',
            '/win32icon:' + ICO, '/resource:' + PNG + ',app.png', '/resource:' + ICO + ',app.ico', '/win32manifest:app.manifest',
            'SetupInfo.cs', 'Setup.cs', 'FolderPicker.cs', 'EmuCore.cs', 'UserData.cs', 'EmuForm.cs']
    r = subprocess.run(args, cwd=SRC, capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stdout + r.stderr)
        return 1
    with open(os.path.join(stage, 'README.txt'), 'w', newline='\r\n') as f:
        f.write('NS2 Emulator Manager\n\n1. Extract this whole zip (right-click > Extract All).\n'
                '2. Run Setup.exe from the extracted folder.\n\nIt installs for your user only (no admin). '
                'Uninstall from Settings > Apps, or with Uninstall.exe in the install folder.\n')
    out = os.path.join(HERE, 'dist', 'NS2EmulatorManager.zip')
    with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
        for n in ('Setup.exe', 'EmuManager.exe', 'README.txt'):
            z.write(os.path.join(stage, n), 'NS2EmulatorManager/' + n)
    old = os.path.join(HERE, 'dist', 'Switch2EmulatorManager-Setup.exe')
    if os.path.exists(old):
        os.remove(old)
    print('installer', out, '%.0f KB' % (os.path.getsize(out) / 1024), r.stdout.strip() or 'no warnings')
    return 0


if __name__ == '__main__':
    cmds = {'build': cmd_build, 'shortcut': cmd_shortcut, 'installer': cmd_installer}
    if len(sys.argv) < 2 or sys.argv[1] not in cmds:
        print(__doc__)
        sys.exit(1)
    sys.exit(cmds[sys.argv[1]](sys.argv[2:]))
