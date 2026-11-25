# Cập nhật gói IIS OEE ngày 02/11/2026

Gói này có Web và DatabaseMigrator riêng. Chỉ cập nhật máy IIS được chọn; không chép database phát triển lên máy vận hành.

## 1. Chuẩn bị trên máy IIS

- Giải nén gói cập nhật vào một thư mục riêng, ví dụ D:\OEE-Update-20261102.
- Trong IIS Manager, chọn đúng Site OEE (ví dụ cổng 8098), xem Physical Path. Đó là WebRoot, không phải thư mục gói mới.
- Sao lưu thư mục Web đang dùng, cấu hình IIS và FULL backup đúng database bằng SSMS. Kiểm tra bản backup trước khi cập nhật.
- Dừng đúng Site/App Pool OEE trước khi chạy migration và chép DLL. Tài khoản chạy migrator cần quyền ALTER database. SQL Integrated Security dùng tài khoản Windows đang chạy PowerShell, không dùng App Pool identity.

## 2. Kiểm tra database, chưa thay đổi dữ liệu

Mở Windows PowerShell tại thư mục gói vừa giải nén:

```powershell
$webRoot = 'D:\OEE'              # Thay bằng Physical Path thực tế trong IIS
$database = 'OeeMasan2'           # Thay bằng tên đúng trong cấu hình: OeeDb hoặc OeeMasan2
.\Update-Database.ps1 -WebRoot $webRoot -ExpectedDatabase $database -VerifyOnly
```

Script in SQL Server, tên database và số migration còn thiếu; dừng nếu tên database khác ExpectedDatabase. Nếu connection string nằm trong appsettings.Production.json hoặc file cấu hình ngoài, thêm -SettingsFile 'đường dẫn file đó'. Không sửa hoặc gửi mật khẩu lên chat. Nếu host dùng biến môi trường ghi đè connection string, xác định đúng cấu hình hiệu lực trước khi chọn file cho migrator.

Trên database local đã kiểm tra ngày 02/11, migration thiếu là 20261013040707_TrackCounterActivity. Máy IIS có thể ở phiên bản khác: kiểm tra kết quả verify và migration history của máy đó trước khi áp dụng.

## 3. Áp dụng migration và kiểm tra

Sau khi đã backup và dừng App Pool:

```powershell
.\Update-Database.ps1 -WebRoot $webRoot -ExpectedDatabase $database
.\Update-Database.ps1 -WebRoot $webRoot -ExpectedDatabase $database -VerifyOnly
```

Nếu đã dùng -SettingsFile ở bước 2, dùng đúng tham số đó cho cả hai lệnh trên. Kết quả cuối phải có Pending migrations: 0. Script chỉ gọi verify/migrate, không seed, reset mật khẩu hoặc tạo lại database. Nếu lỗi thì giữ App Pool dừng và đọc thông báo; không chạy all/seed/initialize-fieldtest để chữa lỗi.

TrackCounterActivity thêm cột nullable datetimeoffset [plc].[CounterCheckpoints].[LastCounterChangedAt], khởi tạo cột mới từ LastReadAt của checkpoint cũ và ghi migration history. LastTotalCount/LastDefectCount và sản lượng cũ giữ nguyên.

Có thể kiểm tra bằng SSMS trên đúng database:

```sql
SELECT DB_NAME() AS CurrentDatabase;
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT name, TYPE_NAME(user_type_id) AS DataType, is_nullable
FROM sys.columns
WHERE object_id = OBJECT_ID(N'plc.CounterCheckpoints')
  AND name = N'LastCounterChangedAt';
```

## 4. Cập nhật Web và chạy lại

- Chép nội dung thư mục Web trong gói vào WebRoot hiện tại, ghi đè DLL và static assets tương ứng.
- Giữ appsettings*.json, web.config riêng, App_Data/storage ngoài, logs và pki/chứng chỉ của máy thật. Gói Web mới chủ động không kèm các file cấu hình này; file web.config tham khảo nằm trong Configuration.
- Khởi động đúng App Pool/Site. Mở /health và trang đăng nhập, nhấn Ctrl+F5. Kiểm tra dropdown bộ lọc kế hoạch, chỉ báo OPC trên thanh tiêu đề, Live OEE và tag/counter thực tế.
- Health Healthy hoặc HTTP 200 chỉ xác nhận host/readiness; cần kiểm tra thời điểm nhận OPC và counter/output để xác nhận thu thập đang hoạt động.

SHA256SUMS.csv ghi checksum các file. File ZIP có SHA256 bên cạnh. Bản cũ và bản backup cần được giữ để đối chiếu; nếu cần khôi phục database phải dùng kế hoạch restore riêng, không chạy Down migration hoặc reset counter tùy tiện.
