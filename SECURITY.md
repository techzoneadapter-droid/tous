# Ghi chú an toàn

## Những gì đã loại bỏ

- Không dùng `AKMasterSocical.exe`, `Update.exe`, `Rar.exe` hoặc `WebDriver.dll` từ gói tham chiếu.
- Không cập nhật nền. Chỉ khi người dùng bấm nút, updater mới lấy release từ repository GitHub cố định `techzoneadapter-droid/tous`, bắt buộc xác minh SHA-256 trước khi thay file và không chạy script từ gói cập nhật.
- Không gửi cookie, proxy, UID, mật khẩu hoặc nhật ký tới máy chủ của nhà phát triển.
- Không tạo tác vụ khởi động, dịch vụ, khóa Registry, scheduled task hay tiến trình nền tồn tại sau khi đóng app.
- Chỉ kết nối tới Facebook, IP API khi người dùng bật kiểm tra, proxy do người dùng cấu hình, và GitHub khi người dùng chủ động kiểm tra cập nhật.

## Nguồn thành phần

- `WebDriver.dll`: NuGet chính thức `Selenium.WebDriver` 4.0.0, target .NET Framework 4.8.
- `chromedriver.exe`: Chrome for Testing chính thức 153.0.8010.37 win32, khớp với Chrome trên máy tại thời điểm build. Binary ChromeDriver 136.0.7103.94 trong gói tham chiếu cũng đã được đối chiếu và trùng hoàn toàn với bản Google phát hành, nhưng không được đưa vào bản build vì đã cũ.
- `app.ico`: tài nguyên hình ảnh được trích từ file tham chiếu; không chứa mã thực thi.

Các hash của bản build nằm trong `SHA256SUMS.txt`.
