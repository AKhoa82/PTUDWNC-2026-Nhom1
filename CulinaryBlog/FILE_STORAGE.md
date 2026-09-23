# Upload / delete MinIO

Module FR-FILE-001/002 được tích hợp trong API. Khi khởi động, API áp dụng migration `AddStoredFileOwnership` để tạo bảng `StoredFiles`. Không cần cherry-pick nhánh MinIO cũ.

## Chạy local

1. Chạy `docker compose up -d postgres redis minio` ở thư mục gốc.
2. Cấu hình `ConnectionStrings__DefaultConnection` trỏ đúng database. Compose mặc định tạo database **CulinaryBlog**, còn appsettings.Development.json hiện dùng **culinary_blog**; đặt biến môi trường nếu sử dụng Compose mặc định.
3. Chạy API với môi trường Development. MinIO local: `localhost:9000`, console: `http://localhost:9001`, tài khoản development theo docker-compose.yml. Bucket ảnh mới: `culinary-blog`.
4. Đăng nhập qua API auth để lấy access token. Gửi `Authorization: Bearer <token>` cho mọi endpoint file.

MinIO production cần cấu hình bằng secret/environment: `MinIO__Endpoint`, `MinIO__AccessKey`, `MinIO__SecretKey`, `MinIO__BucketName`, `MinIO__UseSsl` và tùy chọn `MinIO__PublicBaseUrl` (origin/base path công khai, không kèm bucket/query/fragment). Không dùng tài khoản development trên production. Tài khoản storage cần quyền tạo/kiểm tra bucket, cấu hình public-read, put/get/delete object. Nên dùng bucket riêng cho ảnh công khai; module thiết lập policy GetObject công khai của bucket khi upload lần đầu trong mỗi tiến trình.

## API

| Method | URL | Input | Thành công |
| --- | --- | --- | --- |
| POST | `/api/v1/files/upload?folder=recipes` | multipart field `file`; folder tùy chọn, mặc định `uploads` | 201: `{ url, fileName, contentType, sizeBytes }` |
| DELETE | `/api/v1/files/?fileUrl=<URL-encoded>` | URL chính xác nhận được khi upload | 204 |
| GET | `/api/v1/files/exists?fileUrl=<URL-encoded>` | URL chính xác nhận được khi upload | 200: `{ fileUrl, exists }` |

- Chỉ tài khoản đã xác thực, có user ID hợp lệ và tồn tại trong database được thao tác.
- Chỉ chủ sở hữu được delete/exists. URL công khai không cấp quyền xóa. Không có quyền vượt kiểm tra owner ngầm cho Admin trong module này.
- File được lưu dưới `users/{userId}/{folder}/{uuid}.{ext}`. Folder tối đa 100 ký tự; mỗi segment chỉ chứa ASCII chữ/số, `_`, `-`.
- JPEG/PNG/WebP/AVIF, tối đa 5 MiB; phần mở rộng phải khớp MIME. Kiểm tra signature WebP `RIFF....WEBP`, đủ 8 byte PNG và AVIF major/compatible brand. Đây là kiểm tra signature, không thay thế giải mã toàn bộ ảnh hay quét malware.
- Upload giới hạn 10 request/phút/tài khoản, không xếp hàng. Giới hạn này nằm trong bộ nhớ từng API instance; không phải quota tổng dung lượng. Khi triển khai nhiều instance cần giới hạn dùng chung ở gateway/storage.
- Request upload bị giới hạn tổng 5 MiB + 64 KiB cho multipart overhead; multipart file tối đa 5 MiB.
- File không được đăng ký trong `StoredFiles` trả 404, kể cả file legacy. Cần xác minh và backfill owner tin cậy trước khi cho phép xóa file legacy; không suy đoán owner từ URL gửi lên.
- Xóa lặp lại file của mình trả 204. Bản ghi giữ `DeletedAt` để bảo toàn quyền sở hữu và tính idempotent.
- Lỗi storage trả 503, không lộ exception nội bộ. Lỗi database trả 500; nếu đăng ký upload thất bại, service cố gắng xóa object vừa upload. Nếu dọn dẹp cũng thất bại, log URL orphan để xử lý vận hành.
- Khi MinIO xóa thành công nhưng cập nhật database thất bại, gọi DELETE lại an toàn. Nếu MinIO lỗi, bản ghi chưa bị đánh dấu xóa để có thể retry.
- 400: file/folder/URL sai; 401: chưa xác thực; 403: không có quyền; 404: file chưa đăng ký; 413: request quá lớn; 429: vượt giới hạn upload.

`IFileStorageService` là abstraction nội bộ dành cho caller tin cậy. HTTP endpoint luôn đi qua `FileManagementService` để kiểm tra ownership tại Application Layer. `UploadStreamAsync` cũng áp dụng validation ảnh và giới hạn kích thước. Module này không bổ sung gallery/primary image hoặc Hangfire jobs của FR-RCP-008/FR-JOB.

## Kiểm thử

```powershell
dotnet test CulinaryBlog/CulinaryBlog.Tests/CulinaryBlog.Tests.csproj -m:1 -p:UseSharedCompilation=false
```

Kiểm thử HTTP dùng JWT thực, database in-memory và storage giả. Để kiểm tra migration PostgreSQL và upload/public-read/delete MinIO thật:

```powershell
$env:CULINARY_LIVE_TESTS = '1'
dotnet test CulinaryBlog/CulinaryBlog.Tests/CulinaryBlog.Tests.csproj -m:1 -p:UseSharedCompilation=false
```

Live test tạo database và bucket có tên ngẫu nhiên `culinary_file_test_*` / `culinary-file-test-*`, rồi dọn đúng tài nguyên test đó. PostgreSQL test user cần quyền tạo database. Có thể override kết nối bằng `CULINARY_TEST_POSTGRES`, `CULINARY_TEST_MINIO_ENDPOINT`, `CULINARY_TEST_MINIO_ACCESS_KEY`, `CULINARY_TEST_MINIO_SECRET_KEY`.
