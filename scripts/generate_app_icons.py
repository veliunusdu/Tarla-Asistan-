#!/usr/bin/env python3
"""
Generates all application icons across Android, iOS, macOS, Windows, and Web platforms
using the exact Material Icons grass glyph (Icons.grass) that represents 'Tarlalarım',
rendered in crop green (#2E7D32).
"""

import os
from PIL import Image, ImageFont, ImageDraw

FONT_PATH = "C:/flutter/bin/cache/artifacts/material_fonts/materialicons-regular.otf"
CHAR = chr(0xe2e4) # Icons.grass in MaterialIcons
COLOR_GREEN = (46, 125, 50) # #2E7D32
COLOR_GREEN_RGBA = (46, 125, 50, 255)
COLOR_WHITE = (255, 255, 255)
COLOR_WHITE_RGBA = (255, 255, 255, 255)

WORKSPACE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
MOBILE_DIR = os.path.join(WORKSPACE_ROOT, "mobile")
WEB_DIR = os.path.join(WORKSPACE_ROOT, "web")


def draw_glyph(draw, font, size, fill, y_offset_factor=0.0):
    bbox = draw.textbbox((0, 0), CHAR, font=font)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    x = (size - tw) // 2 - bbox[0]
    y = (size - th) // 2 - bbox[1] + int(size * y_offset_factor)
    draw.text((x, y), CHAR, font=font, fill=fill)


def make_solid_icon(size, padding_ratio=0.32):
    """Square full-bleed icon with solid crop green background and white grass glyph."""
    img = Image.new("RGB", (size, size), COLOR_GREEN)
    draw = ImageDraw.Draw(img)
    glyph_target_size = int(size * (1.0 - padding_ratio))
    font = ImageFont.truetype(FONT_PATH, glyph_target_size)
    draw_glyph(draw, font, size, COLOR_WHITE)
    return img


def make_squircle_icon(size, padding_ratio=0.36, margin_ratio=0.05, radius_ratio=0.22):
    """Rounded squircle icon with transparent padding."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    margin = int(size * margin_ratio)
    radius = int(size * radius_ratio)
    draw.rounded_rectangle([margin, margin, size - margin, size - margin], radius=radius, fill=COLOR_GREEN_RGBA)
    glyph_target_size = int((size - 2 * margin) * (1.0 - padding_ratio))
    font = ImageFont.truetype(FONT_PATH, glyph_target_size)
    draw_glyph(draw, font, size, COLOR_WHITE_RGBA)
    return img


def make_transparent_icon(size, padding_ratio=0.25):
    """Transparent background with green grass glyph."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    glyph_target_size = int(size * (1.0 - padding_ratio))
    font = ImageFont.truetype(FONT_PATH, glyph_target_size)
    draw_glyph(draw, font, size, COLOR_GREEN_RGBA)
    return img


def make_adaptive_foreground(size):
    """Android adaptive icon foreground (108dp canvas, safe zone center 66%)."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    glyph_target_size = int(size * 0.44)
    font = ImageFont.truetype(FONT_PATH, glyph_target_size)
    draw_glyph(draw, font, size, COLOR_WHITE_RGBA)
    return img


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)


def main():
    print("Generating app icons for Tarla Asistanı (Tarlalarım logo: Icons.grass)...")
    assert os.path.exists(FONT_PATH), f"Font file not found: {FONT_PATH}"

    # 1. Mobile assets
    assets_dir = os.path.join(MOBILE_DIR, "assets", "images")
    ensure_dir(assets_dir)
    make_solid_icon(1024).save(os.path.join(assets_dir, "app_logo.png"))
    make_squircle_icon(1024).save(os.path.join(assets_dir, "app_logo_rounded.png"))
    make_transparent_icon(1024).save(os.path.join(assets_dir, "app_logo_transparent.png"))
    print("  [+] Generated mobile assets/images/")

    # 2. Android legacy mipmaps & adaptive icons
    res_dir = os.path.join(MOBILE_DIR, "android", "app", "src", "main", "res")
    android_densities = {
        "mipmap-mdpi": (48, 108),
        "mipmap-hdpi": (72, 162),
        "mipmap-xhdpi": (96, 216),
        "mipmap-xxhdpi": (144, 324),
        "mipmap-xxxhdpi": (192, 432),
    }

    for folder, (legacy_sz, fg_sz) in android_densities.items():
        target_dir = os.path.join(res_dir, folder)
        ensure_dir(target_dir)
        make_squircle_icon(legacy_sz).save(os.path.join(target_dir, "ic_launcher.png"))
        make_adaptive_foreground(fg_sz).save(os.path.join(target_dir, "ic_launcher_foreground.png"))
    print("  [+] Generated Android mipmap icons and adaptive foregrounds")

    # Android adaptive icon XML
    v26_dir = os.path.join(res_dir, "mipmap-anydpi-v26")
    ensure_dir(v26_dir)
    with open(os.path.join(v26_dir, "ic_launcher.xml"), "w", encoding="utf-8") as f:
        f.write(
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">\n'
            '    <background android:drawable="@color/ic_launcher_background"/>\n'
            '    <foreground android:drawable="@mipmap/ic_launcher_foreground"/>\n'
            '</adaptive-icon>\n'
        )

    # Android colors.xml
    values_dir = os.path.join(res_dir, "values")
    ensure_dir(values_dir)
    with open(os.path.join(values_dir, "colors.xml"), "w", encoding="utf-8") as f:
        f.write(
            '<?xml version="1.0" encoding="utf-8"?>\n'
            "<resources>\n"
            '    <color name="ic_launcher_background">#2E7D32</color>\n'
            "</resources>\n"
        )
    print("  [+] Generated Android adaptive icon config (colors.xml, ic_launcher.xml)")

    # 3. iOS AppIcon set
    ios_icon_dir = os.path.join(MOBILE_DIR, "ios", "Runner", "Assets.xcassets", "AppIcon.appiconset")
    ensure_dir(ios_icon_dir)
    ios_sizes = {
        "Icon-App-20x20@1x.png": 20,
        "Icon-App-20x20@2x.png": 40,
        "Icon-App-20x20@3x.png": 60,
        "Icon-App-29x29@1x.png": 29,
        "Icon-App-29x29@2x.png": 58,
        "Icon-App-29x29@3x.png": 87,
        "Icon-App-40x40@1x.png": 40,
        "Icon-App-40x40@2x.png": 80,
        "Icon-App-40x40@3x.png": 120,
        "Icon-App-60x60@2x.png": 120,
        "Icon-App-60x60@3x.png": 180,
        "Icon-App-76x76@1x.png": 76,
        "Icon-App-76x76@2x.png": 152,
        "Icon-App-83.5x83.5@2x.png": 167,
        "Icon-App-1024x1024@1x.png": 1024,
    }
    for fname, sz in ios_sizes.items():
        make_solid_icon(sz).save(os.path.join(ios_icon_dir, fname))
    print("  [+] Generated iOS AppIcon set")

    # 4. iOS LaunchImage
    ios_launch_dir = os.path.join(MOBILE_DIR, "ios", "Runner", "Assets.xcassets", "LaunchImage.imageset")
    ensure_dir(ios_launch_dir)
    make_transparent_icon(140).save(os.path.join(ios_launch_dir, "LaunchImage.png"))
    make_transparent_icon(280).save(os.path.join(ios_launch_dir, "LaunchImage@2x.png"))
    make_transparent_icon(420).save(os.path.join(ios_launch_dir, "LaunchImage@3x.png"))
    print("  [+] Generated iOS LaunchImage set")

    # 5. macOS AppIcon set
    macos_icon_dir = os.path.join(MOBILE_DIR, "macos", "Runner", "Assets.xcassets", "AppIcon.appiconset")
    if os.path.exists(macos_icon_dir):
        macos_sizes = {
            "app_icon_16.png": 16,
            "app_icon_32.png": 32,
            "app_icon_64.png": 64,
            "app_icon_128.png": 128,
            "app_icon_256.png": 256,
            "app_icon_512.png": 512,
            "app_icon_1024.png": 1024,
        }
        for fname, sz in macos_sizes.items():
            make_squircle_icon(sz).save(os.path.join(macos_icon_dir, fname))
        print("  [+] Generated macOS AppIcon set")

    # 6. Mobile Web icons
    web_icons_dir = os.path.join(MOBILE_DIR, "web", "icons")
    ensure_dir(web_icons_dir)
    make_squircle_icon(32).save(os.path.join(MOBILE_DIR, "web", "favicon.png"))
    make_squircle_icon(192).save(os.path.join(web_icons_dir, "Icon-192.png"))
    make_squircle_icon(512).save(os.path.join(web_icons_dir, "Icon-512.png"))
    make_solid_icon(192).save(os.path.join(web_icons_dir, "Icon-maskable-192.png"))
    make_solid_icon(512).save(os.path.join(web_icons_dir, "Icon-maskable-512.png"))
    print("  [+] Generated mobile/web icons and favicon")

    # 7. Windows ICO
    windows_res_dir = os.path.join(MOBILE_DIR, "windows", "runner", "resources")
    if os.path.exists(windows_res_dir):
        ico_master = make_squircle_icon(256)
        ico_master.save(
            os.path.join(windows_res_dir, "app_icon.ico"),
            format="ICO",
            sizes=[(16, 16), (32, 32), (48, 48), (256, 256)],
        )
        print("  [+] Generated Windows app_icon.ico")

    # 8. Web public portal icons
    web_public_dir = os.path.join(WEB_DIR, "public")
    if os.path.exists(web_public_dir):
        ico_master = make_squircle_icon(256)
        ico_master.save(
            os.path.join(web_public_dir, "favicon.ico"),
            format="ICO",
            sizes=[(16, 16), (32, 32), (48, 48)],
        )
        make_squircle_icon(192).save(os.path.join(web_public_dir, "icon.png"))
        make_squircle_icon(180).save(os.path.join(web_public_dir, "apple-icon.png"))
        print("  [+] Generated web/public portal icons")

    print("\nAll app icons successfully generated!")


if __name__ == "__main__":
    main()
