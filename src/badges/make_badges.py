"""Makes the built-in emulators' card logos (src\\badges\\<id>.png, 256 px) from the art in src\\badges\\source:
  eglantine.png   Eglantine's flower (source\\flower.png, from Eglantine's dist\\Eglantine.png)
  pokedinner.png  the flower as a ball inside a ring (source\\pokedinner.html, drawn with Edge)
  lichii.png      Lichii's lychee (source\\lichii.png, from lichii.org)
The flower's see-through lines between the petals and its centre are only a few pixels wide; on a 32 px card badge they
would blur away into white. They are widened here (the petals' colours stay as they are) so the badge colour shows
through at that size, as in Eglantine's own icon.
Run: python src\\badges\\make_badges.py   (needs Pillow and Microsoft Edge), then python manager.py build."""
import os, shutil, subprocess, sys, tempfile, time

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, 'source')
EDGE = [p for p in (os.path.expandvars(r'%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe'),
                    os.path.expandvars(r'%ProgramFiles%\Microsoft\Edge\Application\msedge.exe')) if os.path.exists(p)]
WIDEN = 8   # pixels the see-through lines grow on each side, on the 512 px flower


def widened_flower():
    from PIL import Image, ImageChops, ImageOps
    f = Image.open(os.path.join(SRC, 'flower.png')).convert('RGBA')
    pad = WIDEN * 2
    a = ImageOps.expand(f.getchannel('A'), border=pad, fill=0)
    # shrink the opaque part one pixel at a time, alternating a plus and a square step: an octagon, nearly round
    for i in range(WIDEN):
        if i % 2 == 0:
            s = a
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                s = ImageChops.darker(s, ImageChops.offset(a, dx, dy))
        else:
            s = a
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    if dx or dy:
                        s = ImageChops.darker(s, ImageChops.offset(a, dx, dy))
        a = s
    f.putalpha(a.crop((pad, pad, pad + f.width, pad + f.height)))
    return f


def fit(im, size=256):
    from PIL import Image
    im = im.convert('RGBA')
    r = min(size / im.width, size / im.height)
    im = im.resize((max(1, round(im.width * r)), max(1, round(im.height * r))), Image.LANCZOS)
    c = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    c.paste(im, ((size - im.width) // 2, (size - im.height) // 2), im)
    return c


def edge_render(html, out, size=256):
    from PIL import Image
    tmp = os.path.dirname(html)
    url = 'file:///' + html.replace(os.sep, '/')
    subprocess.run([EDGE[0], '--headless=new', '--disable-gpu', '--hide-scrollbars', '--default-background-color=00000000',
                    '--user-data-dir=' + os.path.join(tmp, 'edge'), '--no-first-run', '--force-device-scale-factor=1',
                    '--window-size=%d,%d' % (size, size), '--screenshot=' + out, url + '?f=0.88&clip=1'], capture_output=True, timeout=60)
    for _ in range(150):   # the picture can land a moment after Edge's launcher returns
        if os.path.exists(out) and os.path.getsize(out) > 0:
            break
        time.sleep(0.1)
    time.sleep(0.3)
    return Image.open(out).convert('RGBA').crop((0, 0, size, size))


def main():
    try:
        import PIL.Image
    except ImportError:
        print('Pillow is needed: python -m pip install pillow')
        return 1
    if not EDGE:
        print('Microsoft Edge is needed to draw the PokeDinner badge')
        return 1
    flower = widened_flower()
    fit(flower).save(os.path.join(HERE, 'eglantine.png'))
    tmp = tempfile.mkdtemp(prefix='ns2badges-')
    flower.save(os.path.join(tmp, 'flower-orig.png'))   # the name pokedinner.html uses
    shutil.copyfile(os.path.join(SRC, 'pokedinner.html'), os.path.join(tmp, 'pokedinner.html'))
    edge_render(os.path.join(tmp, 'pokedinner.html'), os.path.join(tmp, 'pokedinner.png')).save(os.path.join(HERE, 'pokedinner.png'))
    fit(PIL.Image.open(os.path.join(SRC, 'lichii.png'))).save(os.path.join(HERE, 'lichii.png'))
    print('badges: eglantine.png, pokedinner.png, lichii.png')
    return 0


if __name__ == '__main__':
    sys.exit(main())
