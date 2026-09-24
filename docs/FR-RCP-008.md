# FR-RCP-008 — Quản lý hình ảnh công thức

## Thiết lập và chạy

Yêu cầu .NET 10, PostgreSQL, Redis và MinIO. Từ thư mục gốc repository:

```powershell
docker compose up -d postgres redis minio
```

`MinIO:AccessKey`, `MinIO:SecretKey` và `Jwt:Key` không còn chứa giá trị trong appsettings. Cấu hình bằng User Secrets cho development:

```powershell
$apiProject = 'CulinaryBlog/CulinaryBlog.API'
dotnet user-secrets set 'MinIO:AccessKey' '<access-key>' --project $apiProject
dotnet user-secrets set 'MinIO:SecretKey' '<secret-key>' --project $apiProject
dotnet user-secrets set 'Jwt:Key' '<random-signing-key-at-least-32-bytes>' --project $apiProject
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' '<postgres-connection-string>' --project $apiProject
dotnet run --project $apiProject
```

Thay các placeholder bằng cấu hình thực tế; credential MinIO local phải khớp docker-compose.yml. Production dùng biến môi trường `MinIO__AccessKey`, `MinIO__SecretKey`, `Jwt__Key`, `ConnectionStrings__DefaultConnection` và `ConnectionStrings__Redis`. API từ chối khởi động nếu thiếu cấu hình MinIO hoặc khóa JWT dưới 32 byte UTF-8. Không ghi secrets vào Git.

API tự chạy EF migrations lúc khởi động. Migration `20260923161609_AddRecipeImagesAndFileDeletionQueue` tạo gallery và backfill `Recipe.ImageUrl` không rỗng thành ảnh chính; các URL này có `StoredFileId = null`. Không suy đoán quyền sở hữu object từ URL cũ. Các luồng tạo recipe cũ vẫn truyền `ImageUrl` được tạo metadata gallery tương ứng khi lưu.

## API

Các route yêu cầu JWT Bearer và kiểm tra owner/Admin trong Application Layer. Token đăng nhập chứa các role Identity hiện có. Tài khoản đăng ký mới nhận role Author; role Admin phải được cấp qua quy trình quản trị tin cậy. Tài khoản owner cũ chưa có role vẫn quản lý được recipe của mình. Đăng nhập lại sau khi thay đổi role để nhận token mới.

| Method | Route | Input | Success |
| --- | --- | --- | --- |
| POST | `/api/v1/recipes/{id}/images` | multipart: `file`, `altText?`, `isPrimary?` | 201 + RecipeImageDto |
| PATCH | `/api/v1/recipes/{id}/images/{imageId}` | JSON: `altText?`, `isPrimary?`, `orderIndex?` | 200 + RecipeImageDto |
| DELETE | `/api/v1/recipes/{id}/images/{imageId}` | Không nhận URL file từ client | 204 |

Ví dụ upload:

```sh
curl -X POST "$API/api/v1/recipes/$RECIPE_ID/images" \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@dish.webp;type=image/webp" \
  -F "altText=Món ăn hoàn thành" -F "isPrimary=true"
```

Ví dụ PATCH: `{ "isPrimary": true, "altText": "Bữa tối", "orderIndex": 0 }`. `altText: null` hoặc chuỗi trắng xóa mô tả; bỏ field thì giữ nguyên. Cần ít nhất một thay đổi, alt text sau trim tối đa 200 ký tự và orderIndex không âm. Để đổi ảnh chính, gửi `isPrimary: true` cho ảnh thay thế; gửi `false` cho ảnh chính hiện tại trả 422.

`RecipeImageDto` gồm `imageId`, `originalUrl`, `mediumUrl`, `thumbnailUrl`, `altText`, `isPrimary`, `orderIndex`. Detail recipe trả `images` theo OrderIndex rồi Id; `imageUrl` vẫn bằng OriginalUrl của ảnh chính. Medium/thumbnail giữ null, resize FR-JOB-002 chưa thuộc phạm vi này.

Chấp nhận JPEG/PNG/WebP/AVIF với extension, MIME và magic bytes khớp nhau. Giới hạn file chính xác **5 × 1024 × 1024 byte**; request multipart cho phép thêm 64 KiB overhead. File quá 5 MiB trả 400; toàn request quá 5 MiB + 64 KiB bị chặn 413. Upload giới hạn 5 request/phút/IP, lần vượt giới hạn trả 429 kèm Retry-After. Partition dùng RemoteIpAddress; nếu triển khai sau reverse proxy, chỉ cấu hình forwarded headers cho proxy tin cậy để nhận đúng IP client.

Các lỗi còn lại: 401 chưa đăng nhập, 403 không đủ quyền, 404 recipe/ảnh không tồn tại hoặc ảnh thuộc recipe khác, 422 metadata sai, 503 storage/database không khả dụng hoặc chưa xác minh được kết quả upload. Sau 503, client tải lại gallery trước khi chủ động upload lại; không tự retry POST. Các route file tổng quát `/api/v1/files/upload`, `/api/v1/files/exists` và DELETE theo `fileUrl` không được map, trả 404.
`POST /api/v1/recipes` cũng bắt buộc đăng nhập. Handler kiểm tra tài khoản tồn tại, gán author từ caller và ghi yêu cầu invalidate cache trong cùng lần lưu. `ImageUrl` tối đa 500 ký tự. Tài khoản cũ chưa có role Author vẫn tạo được recipe của chính mình.

## Transaction, cache và xóa nền

- Mỗi thao tác khóa hàng recipe bằng PostgreSQL `FOR UPDATE`, đồng bộ giữa các API instance. Unique filtered index chặn hai ảnh chính. Hạ ảnh chính và đặt ảnh mới diễn ra trong một transaction.
- Upload dùng folder cố định `recipes/{recipeId:N}`. Backend sinh object key và lưu `StoredFile=PendingUpload` trước khi gửi MinIO; reservation hết hạn sau 1 giờ. Transaction upload khóa recipe rồi StoredFile và giữ khóa trong quá trình upload/finalize. Thành công chuyển sang Active, gắn RecipeImage và đồng bộ ảnh chính.
- Khi commit lỗi, context/transaction lỗi được dispose; context mới xác minh lại bằng token độc lập, timeout 10 giây. Nếu dữ liệu đã commit, API trả DTO đã lưu và giữ object. Nếu xác nhận không có ảnh liên kết, file chuyển DeletePending để job dọn. Nếu chưa xác minh được, trả 503 và giữ nguyên object; không cleanup dựa trên exception đơn thuần.
- Xóa ảnh chính tự chọn ảnh kế tiếp theo OrderIndex rồi Id; hết ảnh thì ImageUrl = null. URL legacy chỉ xóa metadata.
- Xóa ảnh có StoredFile chuyển DeletePending và ghi DeletionRequestedAt trong cùng transaction với metadata, rồi enqueue bằng StoredFile.Id. HTTP không chờ xóa MinIO. Job khóa StoredFile, kiểm tra không còn ảnh liên kết và xóa bằng BucketName/ObjectKey trong database. Thành công chuyển Deleted và ghi DeletedAt; object đã mất vẫn thành công. File chưa xác minh bucket/key bị chặn xóa.
- Hangfire lưu persistent trong schema `hangfire` của cùng PostgreSQL. Retry **3 lần** sau lần đầu, với delay 60/300/1800 giây. Reconciliation chạy mỗi 5 phút, lấy tối đa 100 yêu cầu chưa có DeletionJobId và enqueue lại. Job đã Failed giữ nguyên để không tự reset giới hạn retry; quản trị viên khắc phục lỗi rồi retry job đó.
- Mỗi lượt reconciliation còn kiểm tra tối đa 100 PendingUpload hết hạn bằng `FOR UPDATE SKIP LOCKED`; bỏ qua file đang khóa và không dọn file còn RecipeImage tham chiếu. Chỉ các upload có bản ghi theo dõi mới thuộc diện tự dọn; object cũ không có bản ghi không bị tự động xóa.
- Enqueue dùng khóa hàng StoredFile và lưu DeletionJobId để hạn chế enqueue trùng. Nếu tiến trình chết giữa enqueue và lưu ID, có thể xuất hiện job trùng; deletion được thiết kế idempotent.
- Mọi mutation ảnh/tạo recipe ghi `RecipeCacheInvalidations` trong cùng transaction. Sau commit, API invalidate tag `recipes` và đổi version danh sách; chỉ đánh dấu ProcessedAt khi cả hai thành công. Worker chạy mỗi phút xử lý lại tối đa 100 yêu cầu còn chờ. Lỗi Redis không biến mutation đã commit thành lỗi giả; cache có thể cũ cho đến khi worker xử lý lại thành công.
- Output cache dùng Redis qua `AddStackExchangeRedisOutputCache`; cache danh sách vẫn dùng IDistributedCache. Các instance cùng môi trường phải dùng chung Redis và `Cache:InstancePrefix` (mặc định `CulinaryBlog:`). Output cache dùng thêm suffix `output:`; môi trường/test khác nhau cần prefix riêng.
- FK cascade xóa metadata khi recipe bị xóa. Khi triển khai luồng xóa toàn bộ recipe, luồng đó cần đánh dấu các StoredFile tương ứng trước khi xóa recipe để reconciliation có thể xử lý object.

Dashboard `/hangfire` bật bằng `Hangfire:DashboardEnabled` (development mặc định bật, production tắt). Toàn bộ route dashboard yêu cầu AdminPolicy. Client truy cập dashboard phải gửi JWT Bearer Admin cho các request, kể cả tài nguyên phụ; đăng nhập qua API không tự tạo cookie cho dashboard.

## Migration, backfill và kiểm kê

Dừng toàn bộ API/worker cũ trước khi triển khai để tránh chạy xen kẽ luồng cleanup cũ. Đặt connection string bằng biến môi trường, áp dụng migration rồi chạy backfill trước khi khởi động API mới:

```powershell
$env:ConnectionStrings__DefaultConnection = '<postgres-connection-string>'
dotnet ef database update --project CulinaryBlog/CulinaryBlog.Infrastructure --startup-project CulinaryBlog/CulinaryBlog.API

# Dry-run mặc định, chỉ xuất báo cáo JSON Lines:
dotnet run --project CulinaryBlog/CulinaryBlog.API -- --storage-backfill

# Áp dụng những ánh xạ xác minh được:
dotnet run --project CulinaryBlog/CulinaryBlog.API -- --storage-backfill --apply

# Kiểm kê object chưa có bucket/key tương ứng trong StoredFiles, chỉ báo cáo:
dotnet run --project CulinaryBlog/CulinaryBlog.API -- --storage-inventory
```

Các lệnh maintenance không mở HTTP hoặc khởi động worker, không tự migrate, không cần JWT key, nhưng cần cấu hình database và MinIO. Migration `AddDurableFileLifecycleAndCacheOutbox` giữ file cũ ở Active/DeletePending/Deleted theo các timestamp trước đó; không coi chúng là upload hết hạn.

Backfill chấp nhận prefix hiện tại và các ánh xạ lịch sử được cấu hình rõ ràng. Ví dụ cấu hình bổ sung (không chứa credential):

```json
{
  "MinIO": {
    "LegacyLocations": [
      { "PublicPrefix": "https://old-images.example.com/culinary-blog/", "BucketName": "culinary-blog" }
    ]
  }
}
```

PublicPrefix phải là URL đầy đủ **bao gồm bucket và dấu `/` cuối**. Chỉ cấu hình prefix/bucket đã xác minh thuộc cùng hệ thống MinIO. Công cụ báo `unresolved` cho URL không xác minh được, `duplicate-reference` cho định danh bị trùng và `invalid-recipe-owner` cho recipe thiếu owner hợp lệ. Không tự gán owner, không tạo StoredFile cho RecipeImage legacy/external. Apply chạy lặp an toàn; xử lý các dòng unresolved/duplicate bằng xác minh dữ liệu trước khi retry job Failed.

Inventory chỉ xuất `untracked-candidate-not-approved-for-deletion`; chưa có StoredFile không có nghĩa object chắc chắn không được tham chiếu. Công cụ không có chế độ xóa. Đổi PublicBaseUrl không đổi định danh xóa; việc cập nhật URL hiển thị cũ hoặc chuyển sang MinIO khác cần kế hoạch dữ liệu riêng.

## Kiểm thử

```powershell
dotnet test CulinaryBlog/CulinaryBlog.sln -m:1

# Bao gồm PostgreSQL + MinIO + Hangfire và khởi động API thật:
$env:CULINARY_LIVE_TESTS = '1'
dotnet test CulinaryBlog/CulinaryBlog.sln -m:1
```

Live tests tạo database `culinary_image_test_*` / `culinary_file_test_*` và bucket riêng, rồi dọn các tài nguyên này; không chạy migration trên database ứng dụng. Cấu hình khác mặc định compose qua `CULINARY_TEST_POSTGRES`, `CULINARY_TEST_REDIS`, `CULINARY_TEST_MINIO_ENDPOINT`, `CULINARY_TEST_MINIO_ACCESS_KEY`, `CULINARY_TEST_MINIO_SECRET_KEY`.

Coverage gồm API auth/owner/Admin, route cũ 404, giới hạn multipart/rate limit, metadata/primary/legacy, migration/backfill/unique index/cascade, cạnh tranh upload/primary, commit mất phản hồi, DB không khả dụng khi xác minh, PUT mất phản hồi, cleanup file đang khóa, idempotence, retry budget và dashboard Admin. Kiểm thử API thật khởi động hai instance, dùng namespace Redis riêng, xác minh invalidation xuyên instance; kiểm thử outbox mô phỏng lỗi cache rồi phục hồi với Redis thật. Bộ test cũng kiểm tra quyền tạo recipe, URL quá dài, kiểm kê không xóa và xóa storage sau khi đổi domain.
