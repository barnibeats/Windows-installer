# Changelog

## 1.0.2 - 2026-10-05

- Fix: the update check failed in WinPE (System.Web.Extensions is missing there). JSON is now read without extra assemblies.
- The disk list falls back to PowerShell/WMI when System.Management is not available.
- Self-test: compares both disk readers and reads the real latest.json.

## 1.0.1 - 2026-10-05

- Fix: the app crashed on start in WinPE (the Capture tab touched table columns that exist only in a normal Windows).
- Developer switch `WI_FORCE_WINPE=1` previews the WinPE screens inside Windows.

## 1.0.0 - 2026-10-05

- First version as an app (replaces the PowerShell scripts): install from ISO/WIM/ESD, capture with Sysprep/DISM, dark/light UI (ru/en/uk), self-update from GitHub releases.
- Verified end to end on a virtual disk: GPT/UEFI (whole disk) and MBR/BIOS (custom size + recovery + Data).
