# Repository Agent Guidelines: Smart Calendar Manager

This document defines architecture invariants, development workflows, quality gates, and release packaging rules for **Smart Calendar Manager**. All agents and automated workflows must strictly adhere to these directives.

---

## 1. Release Invariant: Mandatory Setup Executable

> [!IMPORTANT]
> **Single Deliverable Rule for GitHub Releases:**
> Every release published on GitHub **MUST ALWAYS** include the compiled installer executable (`SmartCalendarManager-Setup-vX.Y.Z.exe`) as an uploaded release asset. Releases without the Setup executable are strictly non-compliant and considered incomplete.

### Packaging Pipeline Specification
1. **Version Bump Order:** Bump the version numbers in:
   - `src/SmartCalendarManager.App/SmartCalendarManager.App.csproj` (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`)
   - `src/SmartCalendarManager.Core/SmartCalendarManager.Core.csproj` (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`)
   - `installer.iss` (`#define MyAppVersion "X.Y.Z"`)
   *Always commit and push version bumps prior to tag creation or binary publication.*
2. **Binary Compilation (`win-x64` self-contained):**
   ```powershell
   dotnet publish src\SmartCalendarManager.App\SmartCalendarManager.App.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true -o releases\SmartCalendarManager-win-x64
   ```
3. **Installer Packaging (Inno Setup):**
   - Use Inno Setup compiler (`ISCC.exe` located at `"$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"` or in `Program Files`):
     ```powershell
     & "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer.iss
     ```
   - Target output: `releases\SmartCalendarManager-Setup-vX.Y.Z.exe`.
4. **Release Asset Upload:**
   ```powershell
   gh release upload vX.Y.Z "releases\SmartCalendarManager-Setup-vX.Y.Z.exe" --clobber
   ```
5. **Asset Verification Gate:**
   - Execute `gh release view vX.Y.Z --json assets`.
   - Verify that `assets.Count > 0` and `SmartCalendarManager-Setup-vX.Y.Z.exe` is present with state `uploaded`.
6. **Local Artifact Cleanliness:**
   - Immediately purge local `releases/` directory contents post-upload to avoid disk bloat:
     ```powershell
     Remove-Item -Recurse -Force releases\* -ErrorAction SilentlyContinue
     ```

---

## 2. Architecture & Modularity

- **`SmartCalendarManager.Core` (.NET 9 Class Library):**
  - Contains all models, services, helpers, and view models.
  - Zero UI dependencies to ensure 100% testability with xUnit.
  - Calendar engine reads RFC 5545 iCal feeds without third-party OAuth.
  - Generates privacy-preserving Google Apps Scripts for work calendar availability blocking.
- **`SmartCalendarManager.App` (WinUI 3 / Windows App SDK):**
  - Unpackaged desktop application (`<WindowsPackageType>None</WindowsPackageType>`).
  - Fluent Design System, Mica backdrop, system tray integration via `H.NotifyIcon.WinUI`.
  - Must compile with `-p:Platform=x64` to prevent architecture mismatch with Windows App SDK.

---

## 3. Desktop Stability Invariants (WinUI 3)

- **Crash Logging Traps:**
  - Global exception handlers (`App.UnhandledException`, CLR `UnhandledException`, `TaskScheduler.UnobservedTaskException`) must write stack traces to `%LOCALAPPDATA%\SmartCalendarManager\Logs\`.
- **XAML Safety:**
  - Never bind `PasswordBox.Password` using `x:Bind Mode=TwoWay` (use code-behind or helper).
  - Avoid dual bindings (`SelectedItem` and `Text` both TwoWay) on editable `ComboBox`.
  - Initialize `App.DispatcherQueue` before instantiating `MainWindow`.

---

## 4. Git & Release Hygiene

- **Conventional Commits:** All commit messages must follow standard Conventional Commits format (`feat:`, `fix:`, `chore:`, `docs:`).
- **No Internal Planning Leakage:** Commit messages, PR titles, and release notes must NEVER contain internal task labels or stage numbers (e.g., `Phase 1`, `Hito 2`, `Option A`).
- **Clean Documentation (Zero Flags):** Never use country flag emojis in `README.md` or any documentation. Use clean text links (`[English](#english) | [Español](#español)`).
