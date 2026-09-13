# Monochrome icon

The active icon is a flat, single-color droplet with a keyhole cutout. No gradients, shadows or highlights. `Build-MonochromeIcon.ps1` creates matching SVG, transparent PNG and multiresolution ICO files from the same geometry.

- `Mizu-Black.svg/png/ico`: window caption, on a light title bar.
- `Mizu-White.svg/png/ico`: executable/taskbar and notification area.
- `Mizu-Monochrome-preview.png`: black-on-white and white-on-black preview.

The older generated blue icon is retained as an unused source asset. Current artwork is editable vector geometry, rendered deterministically rather than image generation.

WindowIcons assigns the small black caption icon and the large white shell icon through WM_SETICON. [Windows icon message documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-geticon).
