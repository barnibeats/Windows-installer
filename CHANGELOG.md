# Changelog

## 1.1.1 - 2026-10-05

- Fix: Windows rejected the generated answer file in the specialize pass ("invalid answer file"). The command that enables the built-in Administrator was longer than the 259 characters the answer file allows; it is shorter now.
- Self-test checks the command length of the generated answer file.

## 1.1.0 - 2026-10-05

- New: Windows settings (answer file + first-logon script). A dialog collects organization, time zone, keyboards, DNS, accounts, auto logon, RDP port, KMS server and system options.
  - Install tab: the settings are written into the installed Windows (`Panther\Unattend\unattend.xml`, `System32\pretail.cmd`).
  - Capture tab: an answer file without passwords and `pretail.cmd` go into the image; accounts and passwords are set at install time.
  - Passwords live in memory only: never saved in a profile, a log or the image; the answer-file copies and the stored auto logon password are removed at the first logon.
  - Profiles can be saved to and loaded from an `.ini` file.
- New: cleanup before Sysprep (DISM component cleanup, temp files, event logs, old Panther logs).
- New: Task Scheduler safety net. Custom tasks are exported before Sysprep and re-created at the first start when missing (users matched by name); the pre-check lists tasks that store a password.
- New: option to merge the desktop shortcuts of all users onto the shared desktop. By default every user keeps their own shortcuts.
- Self-test covers the generated answer file and script.

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
