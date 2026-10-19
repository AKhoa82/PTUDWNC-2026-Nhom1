# FR-JOB-001 — Email chào mừng

## Review và thiết kế

Luồng đăng ký trước đây lưu Identity user/role và refresh token riêng rẽ, chưa gửi email. Thay đổi này đưa user, role Author, refresh token và WelcomeEmailOutbox vào cùng transaction PostgreSQL, dùng chung ApplicationDbContext với Identity. Request/response và frontend không đổi.

Sau commit, dispatcher enqueue WelcomeEmailJob bằng ID outbox. Lỗi enqueue không làm thất bại đăng ký; recurring job `reconcile-welcome-emails` mỗi phút phục hồi tối đa 100 bản ghi Pending, từ cũ đến mới. Dispatcher và sender khóa dòng PostgreSQL để tuần tự hóa các tiến trình. Hangfire chỉ chứa ID outbox, không chứa địa chỉ hoặc nội dung thư.

Trạng thái: Pending=0, Queued=1, Sent=2, Failed=3. UserId có unique index. Retry sau 60, 300, 1800 giây (tối đa 4 lần thử gồm lần đầu). Filter cập nhật Failed sau khi Hangfire quyết định hết retry; recurring job sửa trạng thái nếu filter không cập nhật được. Failed không tự enqueue lại.

SMTP không có transaction chung với database: nếu SMTP nhận thư rồi tiến trình chết trước khi lưu Sent, retry có thể gửi trùng. Nếu dispatcher chết sau enqueue nhưng trước lưu JobId, đối soát có thể tạo thêm job; khóa dòng và kiểm tra Sent ngăn gửi đồng thời hoặc gửi lại sau khi đã lưu Sent.

## Chạy development và nghiệm thu

1. Chạy Docker Desktop, sau đó `docker compose up -d postgres redis minio mailhog`.
2. Cấu hình JWT/MinIO như README. Với môi trường Development, SMTP mặc định localhost:1025, không xác thực/TLS; AppUrl là http://localhost:3000.
3. Apply migration bằng `dotnet ef database update --project CulinaryBlog/CulinaryBlog.Infrastructure --startup-project CulinaryBlog/CulinaryBlog.Infrastructure`. Đặt `ConnectionStrings__DefaultConnection` nếu khác mặc định design-time factory. API cũng áp dụng migration theo luồng khởi động hiện có.
4. Chạy `dotnet run --project CulinaryBlog/CulinaryBlog.API` trong môi trường Development.
5. POST `/api/v1/auth/register` với body hợp lệ và email/username mới. Kiểm tra HTTP 201, access token, refresh token và role Author.
6. Mở http://localhost:8025: thư có tên người dùng, HTML, plain text và link ứng dụng. Outbox chuyển Sent, có SentAt.
7. Dừng MailHog rồi đăng ký tài khoản khác: đăng ký vẫn thành công, job retry theo lịch. Khởi động lại MailHog để lần retry tiếp theo gửi thành công; hoặc chờ hết retry để kiểm tra Failed.
8. Bật `Hangfire__DashboardEnabled=true`, truy cập `/hangfire` bằng tài khoản Admin. Sau khi sửa SMTP, mở Failed job và chọn Retry. Job chấp nhận bản ghi Failed nhưng bỏ qua Sent.

## Production

Đặt credentials bằng User Secrets hoặc biến môi trường, không commit vào Git:

| Biến | Giá trị |
| --- | --- |
| Smtp__Host | SMTP hostname |
| Smtp__Port | 587 cho StartTls hoặc 465 cho SslOnConnect |
| Smtp__Username / Smtp__Password | Cặp credentials; để cả hai rỗng nếu relay không yêu cầu xác thực |
| Smtp__FromEmail | Địa chỉ sender được SMTP provider cho phép |
| Smtp__FromName | Culinary Blog |
| Smtp__Tls | StartTls hoặc SslOnConnect |
| Smtp__AppUrl | URL HTTPS của ứng dụng |

Options được validate khi startup. Production yêu cầu TLS bắt buộc và HTTPS cho AppUrl; chứng chỉ được xác thực theo mặc định MailKit. Development cho phép `Tls=None` và AppUrl HTTP. Không bổ sung xác thực email hay Google OAuth.

Theo dõi Pending lâu bất thường, Hangfire Failed và log theo UserId/OutboxId/JobId. Không ghi credentials/nội dung thư. Lỗi SMTP được thay bằng lỗi tổng quát trước khi đưa vào Hangfire. Outbox chứa tên/email nên cần quyền truy cập database tương ứng dữ liệu tài khoản.

## Kiểm thử

```powershell
dotnet test CulinaryBlog/CulinaryBlog.Application.Tests --filter "FullyQualifiedName~WelcomeEmail"
$env:SEARCH_TEST_POSTGRES = '<connection PostgreSQL test có quyền CREATE DATABASE>'
dotnet test CulinaryBlog/CulinaryBlog.Application.Tests --filter "FullyQualifiedName~WelcomeEmail"
```

Test PostgreSQL tự tạo database `welcome_test_<guid>`, chạy migration và xóa database đó khi hoàn tất. Không dùng database ứng dụng để lưu dữ liệu test. Các tình huống gồm rollback khi lưu outbox/tạo JWT lỗi, đăng ký thành công khi queue lỗi, unique UserId, dispatcher đồng thời, job trùng, phục hồi Pending, Failed và retry thủ công.

Test worker Hangfire thật kiểm tra RetryCount, lịch Scheduled 60/300/1800 giây, Failed và phục hồi thủ công; test chủ động đưa Scheduled về Enqueued để không chờ hết lịch. Test SMTP loopback xác minh thư multipart thực do MailKit gửi và nội dung tiếng Việt; test cấu hình kiểm tra TLS, URL, credentials và HTML encoding.

### Kết quả nghiệm thu Docker — 17/10/2026

- PostgreSQL 16, Redis 7, MinIO và MailHog khởi động thành công; 18/18 test FR-JOB-001 đạt trên PostgreSQL Docker.
- Đăng ký qua HTTP API trả 201, có access/refresh token và role Author. MailHog nhận đúng một email có tiêu đề tiếng Việt, tên người dùng, HTML/plain text và link ứng dụng; outbox Sent, Hangfire Succeeded.
- Dừng MailHog rồi đăng ký: API vẫn trả 201; job chuyển Scheduled, RetryCount=1, lịch retry sau 60 giây. Khởi động lại MailHog: job tự gửi thành công, đúng một thư cho người nhận; outbox Sent, Hangfire Succeeded.
- Nghiệm thu dùng database riêng; API test được dừng và database test được xóa sau khi hoàn tất. Các container được giữ chạy để có thể xem thư tại http://localhost:8025.
