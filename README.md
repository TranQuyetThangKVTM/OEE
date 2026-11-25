# OEE

Hệ thống quản lý hiệu suất thiết bị: .NET 10, Blazor/MudBlazor, SQL Server và OPC UA.

- [Hướng dẫn Siemens Insights Hub OEE](output/word/Huong_dan_Siemens_Insights_Hub_OEE.docx)
- [AGENT.md](AGENT.md) mô tả kiến trúc, luồng dữ liệu và cách kiểm chứng.

Cấu hình SQL Server và OPC UA theo môi trường. Khai báo `ConnectionStrings__DefaultConnection` bằng biến môi trường hoặc dùng user secrets cho máy phát triển. Các script kiểm tra giao diện nhận `OEE_UI_USER` và `OEE_UI_PASSWORD` từ môi trường.

Cấu trúc chính: `src/Oee.Domain` giữ quy tắc; `src/Oee.Application` điều phối use case; `src/Oee.Infrastructure` cung cấp SQL/OPC/worker; `src/Oee.Web` chứa UI/API và cấu hình chạy.

```powershell
dotnet restore OeeMasanNext.sln
dotnet build OeeMasanNext.sln -c Release
dotnet test OeeMasanNext.sln -c Release --no-build
```

Kiểm thử SQL cần database kiểm thử riêng và `OEE_TEST_SQLSERVER`. Không dùng database vận hành cho kiểm thử.

Cập nhật IIS theo [hướng dẫn IIS](deployment/iis/README-IIS.md). Giữ cấu hình thực tế, `App_Data`/storage ngoài, chứng chỉ và dữ liệu SQL. Không restore database local đè lên hệ thống thật khi cập nhật Web.
