# Repository working conventions

For every user-facing update, including documentation changes:

1. Build and run the automated tests appropriate to the change.
2. Publish a GitHub Release with a complete Windows portable ZIP and SHA-256 file. The ZIP must contain the matching source archive and license.
3. The release workflow runs on each push to master. Verify that its release asset was uploaded; a successful push alone is not a completed release.
4. If publication fails, fix the build or publishing problem and report the real status. Never claim an asset exists without checking it.
5. Preserve historical tags and releases. Do not retag old versions to newer code; create a new version instead.

Keep English and Simplified Chinese READMEs consistent with the actual desktop features. Never fabricate Steam unlock timestamps. Automated tests must not modify a real Steam account.
