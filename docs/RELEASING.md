# Release procedure

The monitor version is **2.1.0** (`v2.1.0` tag, `2.1.0.0` assembly/file/manifest version). The independent VS Code extension remains **1.1.1**. Signing is optional; never describe an unsigned asset as signed.

1. Start from a clean, committed tree. Push it and require successful Windows CI for the exact commit. The full gate must reach `RELEASE_GATE_OK`; all nine render cases must pass in both languages (18 cases), along with localization and settings migration checks.
2. Build the candidate separately, embedding that source commit:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Release -ReleaseTag v2.1.0 -OutputPath .\artifacts\build\CodexTokenMonitor.exe
   ```

3. Package and verify the exact candidate, rather than rebuilding test assemblies as a substitute:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-release.ps1 -ReleaseTag v2.1.0
   ```

   This verifies the built assembly and real startup process using temporary logs/settings, checks EXE/manifest/source versions and the VSIX contents, and creates `SHA256SUMS.txt`, `release.json`, and English release notes. The candidate directory contains the four upload assets: EXE, VSIX, checksum file, and metadata. Release notes are supplied as the GitHub release body.

4. Recheck successful CI for the recorded commit and that the remote `main` points to it. Create a **draft** Release targeting that commit. If the tag exists, require it to resolve to the same commit. Upload the exact checked assets without rebuilding them.
5. Download assets from the draft to a separate folder. Recompute all three hashes listed in `SHA256SUMS.txt`, check embedded commit and versions, and repeat binary/VSIX verification. Only then publish the Release.
6. Verify the public assets, tag/commit, SHA-256, both README download links, `main` CI badge, Description, and Topics. Do not replace released assets silently; use a new version for corrections.

The first Release has no Authenticode signature. SHA-256 verifies file identity, not publisher identity. Optional signing uses `build.ps1 -Production`, requires a tag on `HEAD` and a valid signing certificate, and must occur before the final checksum/verification steps.

The authoritative versioned downloads are Release assets; no EXE is tracked in the source repository. The default build and packaging directory is `artifacts/build/`, which Git ignores. For development beside a running release, choose a separate `-OutputPath`. Standard application data remains `%LOCALAPPDATA%\CodexTokenMonitor`; all verification uses isolated directories and never reads personal Codex logs.

CI runs the full test gate on every commit. It also builds and verifies a release candidate when that version's tag does not exist yet or points to the tested commit. Commits after publication run development builds and tests without repackaging the existing release. Increase the application version before preparing another release.
