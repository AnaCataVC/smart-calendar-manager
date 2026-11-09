# Repository Agent Guidelines: Smart Calendar Manager

This document defines architecture invariants, development workflows, quality gates, and release packaging rules for **Smart Calendar Manager**. All agents and automated workflows must strictly adhere to these directives.

---

## 1. Release Invariant: Mandatory Setup Executable (Single Deliverable Policy)

> [!IMPORTANT]
> **Single Deliverable Rule for GitHub Releases:**
> Every release published on GitHub **MUST ALWAYS** publish the official Windows Setup installer executable along with its cryptographic SHA-256 hash:
> ```text
> SmartCalendarManager-Setup-vX.Y.Z.exe
> SmartCalendarManager-Setup-vX.Y.Z.exe.sha256
> ```
> - **Automated Release via CI/CD**: Pushing a tag (`git tag vX.Y.Z && git push origin vX.Y.Z`) automatically triggers `.github/workflows/release.yml` on `windows-latest`, which tests, publishes the self-contained app, compiles Inno Setup with `/DMyAppVersion=X.Y.Z`, computes the `.sha256` checksum, and creates the GitHub Release with release notes.
> - **Inno Setup Macro Standard**: `installer.iss` MUST define `MyAppVersion` within `#ifndef MyAppVersion` so CI CLI arguments override the version dynamically without file mutations.
> - **Strict Prohibition on Extra Assets**: NEVER publish standalone portable executables (`.exe`), unpackaged binaries, or compressed archives (`.zip`) as GitHub Release assets. Production binaries compiled during build stages exist strictly to be packaged into the Setup installer or for test execution, never as independent downloads.
> - **Verification Invariant**: All release workflows and subagents (`ami-release-manager`) must verify via `gh release view <tag> --json assets` that only the Setup executable and its `.sha256` hash are present. Releases lacking the Setup executable or containing portable/zip files are strictly non-compliant.

### Packaging Pipeline Specification
1. **Version Bump Order:** Bump the version numbers in:
   - `src/SmartCalendarManager.App/SmartCalendarManager.App.csproj` (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`)
   - `src/SmartCalendarManager.Core/SmartCalendarManager.Core.csproj` (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`)
   - `installer.iss` (`#ifndef MyAppVersion #define MyAppVersion "X.Y.Z" #endif`)
   *Always commit and push version bumps prior to tag creation.*
2. **Automated Remote Release (Recommended):**
   ```powershell
   git tag vX.Y.Z
   git push origin vX.Y.Z
   gh run watch (gh run list --workflow=release.yml --limit 1 --json databaseId -q '.[0].databaseId')
   ```
3. **Local Compilation & Packaging (Fallback):**
   - Binary Compilation (`win-x64` self-contained):
     ```powershell
     dotnet publish src\SmartCalendarManager.App\SmartCalendarManager.App.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true -o releases\SmartCalendarManager-win-x64
     ```
   - Installer Packaging (Inno Setup):
     ```powershell
     & "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" /DMyAppVersion=X.Y.Z installer.iss
     Get-FileHash releases\SmartCalendarManager-Setup-vX.Y.Z.exe -Algorithm SHA256 | ForEach-Object { "$($_.Hash)  $($_.Path | Split-Path -Leaf)" } | Out-File -Encoding ascii releases\SmartCalendarManager-Setup-vX.Y.Z.exe.sha256
     ```
4. **Asset Verification Gate:**
   - Execute `gh release view vX.Y.Z --json assets`.
   - Verify that `assets.Count >= 2` and `SmartCalendarManager-Setup-vX.Y.Z.exe` + `.sha256` are present with state `uploaded`.
5. **Local Artifact Cleanliness:**
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
- **System Tray & Window Lifecycle Invariants:**
  - In WinUI 3, `{x:Bind}` on a `Window` class only evaluates on `Window.Activated`. When launching minimized directly to the system tray (`AppWindow.Hide()`), `Window.Activate()` is never called, leaving `{x:Bind}` expressions uninitialized (`null`).
  - `MainWindow` must call `Bindings.Initialize()` explicitly in its constructor and assign `Command` and `Click` handlers directly in C# code-behind for tray context menu items to guarantee compatibility with native Win32 popup dispatching (`H.NotifyIcon`).

---

## 4. Calendar Engine & Pre-Meeting Automation Invariants

- **Just-In-Time (JIT) Verification Gate:**
  - Pre-meeting timers (`System.Threading.Timer`) MUST NEVER trigger external app launches (such as Granola) using stale cached snapshots.
  - Upon timer expiration, the service must perform a live refresh against the feed to verify that the target event still exists today, retains the exact same `StartTime`, is not cancelled (`STATUS:CANCELLED`), and was not declined (`PARTSTAT=DECLINED`). If any attribute changed, the trigger must be aborted and timers rescheduled.
- **RFC 5545 Millisecond Truncation in Unit Tests:**
  - iCal `DTSTART`/`DTEND` timestamps have 1-second resolution (`yyyyMMddTHHmmss`) and discard millisecond fractions. Unit tests verifying timers or scheduled triggers must never use sub-second offsets (`AddMilliseconds`); always specify deltas of at least 1-2 whole seconds to prevent false triggers during setup.

---

## 5. Git & Release Hygiene

- **Conventional Commits:** All commit messages must follow standard Conventional Commits format (`feat:`, `fix:`, `chore:`, `docs:`).
- **No Internal Planning Leakage:** Commit messages, PR titles, and release notes must NEVER contain internal task labels or stage numbers (e.g., `Phase 1`, `Hito 2`, `Option A`).
- **Clean Documentation (Zero Flags):** Never use country flag emojis in `README.md` or any documentation. Use clean text links (`[English](#english) | [Español](#español)`).
