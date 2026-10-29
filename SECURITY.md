# Security policy

## Supported versions

Security fixes are provided for the latest release only.

## Reporting a vulnerability

Please do **not** report security issues in public issues.

Use GitHub's private vulnerability reporting instead:
<https://github.com/memoldinyo/IP-Switch/security/advisories/new>

Please include the IP-Switch version, the Windows version and the steps to reproduce. You will receive a response
as soon as possible; confirmed issues are fixed in a new release and mentioned in the [changelog](CHANGELOG.md).

## Verifying downloads

- Releases are built from the tagged source code by GitHub Actions.
- Signed releases carry an Authenticode signature with a certificate issued to SignPath Foundation (see the
  [code signing policy](README.md#code-signing-policy)). Check it in the file properties under *Digital Signatures*.
- Every release contains `SHA256SUMS.txt`. Compare with:

  ```powershell
  Get-FileHash .\IPSwitch-<version>-x64.msi -Algorithm SHA256
  ```
