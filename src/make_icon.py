"""Draws the manager's icon (src\\icon.html) with Edge and writes src\\app.ico (the console alone, every size: the
desktop, Start and taskbar icon) and src\\app.png (the same console, 256 px: the logo in the window and in Setup).
Run: python src\\make_icon.py   (or delete app.ico / app.png: manager.py build runs it)."""
import os, struct, subprocess, sys, tempfile, time

SRC = os.path.dirname(os.path.abspath(__file__))
SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
EDGE = [p for p in (os.path.expandvars(r'%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe'),
                    os.path.expandvars(r'%ProgramFiles%\Microsoft\Edge\Application\msedge.exe')) if os.path.exists(p)]


def render(size, out, profile, tile):
    from PIL import Image
    url = 'file:///' + os.path.join(SRC, 'icon.html').replace(os.sep, '/') + '?size=%d&tile=%d' % (size, 1 if tile else 0)
    # Edge's window can't be smaller than its minimum: draw on a larger page and keep the top left corner.
    # Its own profile folder, so the user's open Edge is never involved.
    page = max(size, 256)
    subprocess.run([EDGE[0], '--headless=new', '--disable-gpu', '--hide-scrollbars', '--default-background-color=00000000',
                    '--user-data-dir=' + profile, '--no-first-run', '--force-device-scale-factor=1',
                    '--window-size=%d,%d' % (page, page), '--screenshot=' + out, url], capture_output=True, timeout=60)
    for _ in range(150):   # the picture can land a moment after Edge's launcher returns
        if os.path.exists(out) and os.path.getsize(out) > 0:
            break
        time.sleep(0.1)
    time.sleep(0.3)
    im = Image.open(out).convert('RGBA').crop((0, 0, size, size))
    im.save(out)


def main():
    if not EDGE:
        print('Microsoft Edge is needed to draw the icon')
        return 1
    try:
        import PIL.Image
    except ImportError:
        print('Pillow is needed to draw the icon: python -m pip install pillow')
        return 1
    tmp = tempfile.mkdtemp(prefix='ns2icon-')
    pngs = []
    for n in SIZES:
        f = os.path.join(tmp, '%d.png' % n)
        render(n, f, os.path.join(tmp, 'edge'), False)
        pngs.append((n, open(f, 'rb').read()))
    # .ico with PNG images (Windows Vista and later read these at every size)
    head = struct.pack('<HHH', 0, 1, len(pngs))
    off = 6 + 16 * len(pngs)
    entries, data = b'', b''
    for n, b in pngs:
        entries += struct.pack('<BBBBHHII', n % 256, n % 256, 0, 0, 1, 32, len(b), off + len(data))
        data += b
    open(os.path.join(SRC, 'app.ico'), 'wb').write(head + entries + data)
    logo = os.path.join(tmp, 'logo.png')
    render(256, logo, os.path.join(tmp, 'edge'), False)
    open(os.path.join(SRC, 'app.png'), 'wb').write(open(logo, 'rb').read())
    print('icon: app.ico (%s px) and app.png' % ', '.join(str(n) for n in SIZES))
    return 0


if __name__ == '__main__':
    sys.exit(main())
