# DupeXDupe

DupeXDupe is a Windows desktop (C# WinForms) duplicate-file explorer focused on practical cleanup of large drives.

## What It Does
- Scans a selected drive/folder.
- Finds duplicate candidates using `file size + filename`.
- Groups duplicates for bulk review.
- Lets you sort, filter, auto-select, and delete selected duplicates.
- Deletes to **Recycle Bin** (not hard delete).

## Why This Approach
The project originally used deep hashing. On large HDDs this caused very heavy disk reads and long UI stalls. We changed to a faster operational model:
- **Match strategy**: `size + name` only.
- **Reason**: much faster for large media libraries and backup-style folder structures.
- **Tradeoff accepted**: small risk of false positives vs. much faster throughput.

## Core Features
- Path picker (`Browse`) + scan start.
- Cancel scan.
- Sort modes: `Biggest`, `Oldest`, `Newest`, `Name`, `Group`.
- Top-N workflow:
  - Enter a number and click `Select + Show Top N`.
  - `0` or blank means all groups.
  - `Show All Groups` removes the Top-N filter.
- Selection helpers:
  - `Auto-Select (Keep 1)`
  - `Clear Selection`
- Delete workflow:
  - `Delete Selected` runs in background
  - Busy/progress indicator shown
  - Files sent to Recycle Bin

## Mother-Cluster System (Path Consistency)
This was added to avoid random keep-locations across repeated folder patterns.

How it works:
1. Duplicate groups are clustered by shared directory signature.
2. Each cluster gets a color.
3. A mother path is chosen for the cluster.
4. Keep/delete selection is propagated consistently across groups in that cluster.
5. If you change keep choice in one group, the cluster mother updates and related groups follow.

Result: consistent target path behavior across many similar duplicate sets.

## UX/Performance Improvements Implemented
- Delete runs off UI thread (prevents hard freeze during large deletes).
- Busy throbber + progress text during delete.
- Group coloring strengthened and reapplied after binding/filter operations.

## App Naming Changes
- Product/app naming updated to **DupeXDupe**.
- Window title updated to **DupeXDupe - Visual Duplicate Explorer**.
- Assembly/product metadata updated to DupeXDupe.

## Build and Run
From repository root:

```powershell
cd DupeFinder.App
dotnet build
dotnet run
```

Publish single-file EXE:

```powershell
cd DupeFinder.App
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

## Current Publish Output
After publish, EXE is at:

`DupeFinder.App\\bin\\Release\\net8.0-windows\\win-x64\\publish\\DupeXDupe.exe`

## Notes
- Protected/system locations may be skipped if access is denied.
- Recycle Bin behavior is per-drive (`$Recycle.Bin` on that volume).
- For very large HDD jobs, deletion can still take time due to filesystem + Recycle Bin overhead.
