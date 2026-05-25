# Trust And Security

`kmb file tools` is a free, open-source, local Windows utility. Current public beta builds are unsigned.

## Official Download Sources

Use official links only:

- GitHub Releases: https://github.com/KiritoMainBro88/snappy-docs-convert/releases
- Current beta: https://github.com/KiritoMainBro88/snappy-docs-convert/releases/tag/v0.1.0-beta.1
- Website: https://website-sand-xi-15.vercel.app

The website links to GitHub Releases. It does not host a separate download mirror.

## Unsigned App Limitation

Current beta builds are unsigned. Windows SmartScreen or Unknown Publisher warnings may appear.

This warning does not automatically mean malware. It means Windows does not see a trusted code-signing certificate or established publisher reputation for the file.

No free method guarantees zero SmartScreen or antivirus warnings. This project does not claim code signing yet.

## What Users Should Verify

Before running a download:

1. Confirm the URL is an official GitHub Release URL.
2. Confirm the asset name matches the release notes.
3. Confirm the SHA256 checksum matches the release notes or `SHA256SUMS.txt`.
4. Review the public source code if needed.
5. Check release notes and known limitations.

Do not run files from unofficial mirrors. Do not disable antivirus globally. Only proceed if the file hash matches the official release and you trust the source/release.

## What The Project Does For Trust

- Local-only processing.
- No upload.
- No telemetry, analytics, tracking, or remote converter service.
- Public source code.
- Build and packaging scripts in the repository.
- GitHub Actions CI for build/test/website validation.
- Release build workflow for portable ZIP and installer EXE.
- SHA256 checksum manifests.
- Planned GitHub artifact attestations for release-build workflow artifacts.

## What The Project Does Not Do

- No code signing yet.
- No paid certificate yet.
- No guarantee that every antivirus vendor will classify each beta build cleanly immediately.
- No obfuscation, packing, or UPX.
- No automatic upload to third-party scanners.

## False Positive Process

If Microsoft Defender flags an official release:

1. Keep the file.
2. Copy the SHA256 hash.
3. Submit the file to Microsoft Security Intelligence:
   https://www.microsoft.com/en-us/wdsi/filesubmission
4. Open a GitHub issue with:
   - app version
   - asset used
   - SHA256
   - antivirus vendor
   - detection name
   - screenshot/log
   - download source URL
   - whether the hash matched the official release
   - OS version

For other antivirus vendors, submit the file to that vendor's false-positive portal and open a GitHub issue with the same evidence.

## Vietnamese / Tiếng Việt

### Cảnh báo SmartScreen/Unknown Publisher

Bản beta hiện tại chưa được ký số. Windows SmartScreen hoặc cảnh báo Unknown Publisher có thể xuất hiện.

Cảnh báo này không tự động có nghĩa là malware. Nó thường có nghĩa là file chưa có chứng chỉ ký số đáng tin cậy hoặc chưa có uy tín publisher đủ lâu.

Không có cách miễn phí nào bảo đảm mọi cảnh báo sẽ biến mất. Dự án không tuyên bố đã code signing.

### Cách kiểm tra file chính thức

1. Chỉ tải từ GitHub Releases chính thức.
2. Kiểm tra tên asset đúng với release notes.
3. Kiểm tra SHA256 khớp với release notes hoặc `SHA256SUMS.txt`.
4. Xem mã nguồn công khai nếu cần.
5. Đọc release notes và giới hạn hiện tại.

Không chạy file từ mirror không chính thức. Không tắt antivirus toàn hệ thống. Chỉ tiếp tục nếu hash khớp bản phát hành chính thức và bạn tin nguồn tải.

### Cách báo false positive

Nếu Microsoft Defender báo lỗi:

1. Lấy SHA256 của file.
2. Gửi file lên trang Microsoft Security Intelligence:
   https://www.microsoft.com/en-us/wdsi/filesubmission
3. Mở GitHub issue với detection name, hash, vendor, screenshot/log, URL tải, OS version.

Nếu antivirus khác báo lỗi, gửi false positive cho vendor đó và mở GitHub issue kèm bằng chứng.
