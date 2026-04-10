# Physical-KB-LayoutSwitcher

The only existing layout manager that achieved my goal was incompatible with windows 10 and fixing was making it worse so I did a mostly full rewrite taking in it's method. there were no other KBMs that would take a multitude of keyboards and make each one have it's own layout, I needed a seperate layout for Dvorak, Russian & Qwerty on the 3 keyboards I have on my desk so I built this tool to handle that.

## Goal

Use raw keyboard device input to identify which physical keyboard generated the latest key press, then switch the active keyboard layout to that keyboard's assigned layout.

## Why A Rewrite

The older codebase can detect physical keyboards and save mappings, but its layout-switching behavior relies on Win32 patterns that no longer behave reliably on current Windows versions.

## Acknowledgements

This project was informed by the earlier [RightKeyboard](https://github.com/agabor/RightKeyboard) utility and its work on per-keyboard layout switching on Windows.

## Current Status

- Modern .NET WinForms tray app scaffolded
- JSON config persistence added
- Raw input listener working with per-device debug logging
- First-pass device-to-layout mapping flow added
- Layout switching service wired to mapped keyboard activity
- Auto-start enabled through the current user's Windows sign-in registry key

## Known Issues

- Layout switching can still be unreliable when moving between same-language variants, especially `US QWERTY <-> Dvorak` style changes.

## Planned Pieces

- Raw input keyboard device listener
- Device-to-layout assignment UI
- Safe layout switch service for modern Windows
- Debug logging for device IDs and switch events
- Auto-start and reconnect handling
- Package the app for later easy free distribution
