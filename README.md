# AKMasterSocical Clean Edition

Ứng dụng Windows xử lý cục bộ để tạo môi trường quốc gia cho một hoặc nhiều phiên Facebook bằng Chrome. Bản này được viết lại từ đầu và **không tái sử dụng mã của file chạy gốc**.

## Chức năng

- Nhập từng tài khoản hoặc nhập TXT/CSV theo định dạng `UID|COOKIE|PROXY|MÃ_QUỐC_GIA`.
- Tự nhận UID từ trường `c_user` trong cookie.
- Chạy nhiều phiên với giới hạn luồng, độ trễ và thời gian duy trì tùy chỉnh.
- Áp dụng proxy, ngôn ngữ/locale, múi giờ và tọa độ trình duyệt theo quốc gia.
- Kiểm tra quốc gia IP qua `https://ipapi.co/country/` (có thể tắt).
- Đăng nhập Facebook bằng cookie hoặc để người dùng đăng nhập thủ công.
- Dừng toàn bộ trình duyệt đang chạy và xuất kết quả CSV không chứa cookie.
- Nút **KIỂM TRA CẬP NHẬT** tải bản phát hành mới từ GitHub chính thức, xác minh SHA-256 rồi tự cài và mở lại app.

## Sử dụng

1. Mở `AKMasterSocical.exe` trong thư mục `dist\AKMasterSocical-Clean`.
2. Thêm tài khoản hoặc nhập TXT. Nếu không muốn dùng cookie, để trống và đăng nhập thủ công.
3. Chọn quốc gia, proxy và cấu hình chạy.
4. Bấm **BẮT ĐẦU**.

Khi có bản mới, bấm **KIỂM TRA CẬP NHẬT** ở góc phải. Updater chỉ kết nối đến API và file phát hành của repository `techzoneadapter-droid/tous`.

## Phát hành phiên bản mới

1. Tăng phiên bản trong `src/Program.cs`, `src/Updater.cs` và `installer.iss`.
2. Commit thay đổi rồi tạo tag dạng `v1.0.0.19` và push tag lên GitHub.
3. GitHub Actions tự build bộ cài, tạo ZIP + SHA-256 và đăng chúng vào Releases. Các máy đã cài chỉ cần bấm **KIỂM TRA CẬP NHẬT**.

Chrome phải được cài trên máy. ChromeDriver đi kèm là bản 153.0.8010.37 chính chủ, khớp với Chrome trên máy tại thời điểm build; nếu Chrome đã nâng cấp và báo không tương thích, thay `chromedriver.exe` bằng bản tương ứng từ Chrome for Testing.

## Giới hạn quan trọng

Ứng dụng chỉ thay đổi tín hiệu môi trường của phiên Chrome (IP qua proxy, locale, múi giờ, geolocation). Facebook tự quyết định và có thể không thay đổi “quốc gia chính” của tài khoản. Ứng dụng không vượt checkpoint, 2FA, xác minh danh tính hay chính sách nền tảng.

## Quyền riêng tư

- Cookie chỉ giữ trong bộ nhớ trong lúc app chạy; không ghi vào file cấu hình.
- Hồ sơ Chrome tạm được xóa sau phiên nếu không chọn giữ trình duyệt.
- Kết quả xuất không chứa cookie và che thông tin đăng nhập proxy.
- Không có updater, telemetry, bot, lệnh từ xa, persistence hoặc endpoint bí mật.
