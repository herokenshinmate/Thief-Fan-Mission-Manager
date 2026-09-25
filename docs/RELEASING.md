# Releasing Thief FM Manager

Releases are built and published by GitHub Actions (`.github/workflows/release.yml`) when a version tag is pushed. The workflow tests the app, builds a self-contained Windows installer with [Velopack](https://velopack.io), and publishes a GitHub Release that installed copies update from.

## Repository visibility

The repository must be public: installed copies check for updates anonymously, and users download the installer from Releases.

## Cutting a release

1. Bump `AppVersion.Current` in `src/ThiefManager/AppVersion.cs`. Use a patch bump for normal changes, e.g. `3.9.7` → `3.9.8`.
2. Add a matching entry at the top of `ChangelogData.Entries` in `src/ThiefManager/ChangelogEntry.cs`. Its bullets become the GitHub release notes and the in-app "What's new" text.
3. Commit, and push to `main`.
4. Tag and push:
   ```
   git tag v3.9.8
   git push origin v3.9.8
   ```
5. Watch the **Release** workflow on the repository's Actions tab. When it finishes, the release appears under Releases with:
   - `ThiefFMManager-win-Setup.exe`: the installer for new users.
   - `ThiefFMManager-win-Portable.zip`: a no-install portable copy.
   - Full and delta `.nupkg` packages, the `releases.win.json` feed, and `assets.win.json`, which installed copies use to update.

The workflow stops before building if:
- the tag doesn't match `AppVersion.Current`, or the changelog has no entry for that version;
- any test fails.

To re-run a failed release without re-tagging, use **Run workflow** on the Release workflow and enter the existing tag. If a failed run left a draft or partial release for the tag, delete that release (not the tag) on GitHub first; the upload won't overwrite an existing release.

You can check the version and changelog locally before tagging:

    powershell -File build/Get-ReleaseNotes.ps1 -Version 3.9.8

## Code signing (optional, currently off)

Builds are currently unsigned, so Windows SmartScreen warns on first install. To sign them, add a repository secret named `SIGN_PARAMS`, containing the `signtool.exe` arguments Velopack should use. A certificate file isn't available on the build runner, so `SIGN_PARAMS` must use a signing method available there, such as a certificate installed in the runner's store or a cloud signing service's signtool parameters.

The pack step then signs automatically. See `vpk pack --help` (`--signParams`) and the Velopack signing docs for other signing services.
