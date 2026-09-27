# FR-SRCH-002 — Lọc công thức (backend)

## Phạm vi

Hoàn thiện API danh sách hiện có theo FR-RCP-001, FR-SRCH-002 và mô hình dữ liệu 7.2 trong SRS.md. Giao diện được giữ nguyên và sẽ triển khai sau. Chưa tích hợp với endpoint full-text search của FR-SRCH-001.

## Hợp đồng API

`GET /api/v1/recipes`

| Tham số tùy chọn | Điều kiện |
| --- | --- |
| `categoryId` | GUID; lọc đúng danh mục. GUID không tồn tại trả 200 với items rỗng. |
| `difficulty` | Easy, Medium, Hard, Expert; không phân biệt hoa/thường. Giữ tương thích số enum 1–4. |
| `maxCookTime` | Số nguyên từ 0 đến int.MaxValue; CookingTimeMinutes <= ngưỡng, không cộng thời gian chuẩn bị. |
| `minServings` | Số nguyên từ 1 đến int.MaxValue; Servings >= ngưỡng. |

Không truyền tham số nghĩa là không lọc theo tiêu chí đó. Các điều kiện kết hợp AND và kết hợp với keyword hiện có. Giữ Expert theo mô hình dữ liệu 7.2 và enum hiện hành, dù bảng FR-SRCH-002 chỉ liệt kê ba mức. Cho phép maxCookTime=0 để hỗ trợ món không cần nấu.

Ví dụ:

```http
GET /api/v1/recipes?difficulty=Easy&maxCookTime=30&minServings=4&page=1&pageSize=12
```

Giữ PagedResult, sort mặc định -createdAt, page=1, pageSize=12 (tối đa 50). Quyền đọc được áp dụng trước các bộ lọc; COUNT thực hiện trước phân trang. Guest chỉ thấy Published; Author thấy thêm Draft/Archived của chính mình; Admin thấy tất cả.

Lỗi miền giá trị trả 422, bao gồm maxCookTime âm, minServings nhỏ hơn 1, difficulty ngoài enum. Lỗi bind kiểu (GUID sai, số thập phân, số vượt int) do Minimal API xử lý với 400. FluentValidation của truy vấn được chuyển thành ValidationProblem 422.

## Cache và tương thích

MinServings được thêm cuối GetRecipesQuery với mặc định null. Cache key chứa cả bốn tiêu chí; giữ TTL 15 phút, version database và fallback khi Redis lỗi. Thay đổi Recipe qua ApplicationDbContext tiếp tục đổi version, bao gồm sửa khẩu phần/thời gian nấu. Không cần migration mới. Database vẫn cần các migration hiện có, bao gồm RecipeListVersions.

## Kiểm thử

RecipeFilterTests kiểm tra từng tiêu chí bằng ID mong đợi, đủ bốn mức độ khó, biên 0 phút/1 khẩu phần, AND với keyword, metadata phân trang, quyền Guest/Author/Admin, validation, phân biệt cache và invalidation khi sửa khẩu phần/thời gian. Test PostgreSQL/Redis hiện có được mở rộng với bốn bộ lọc và cache sau cập nhật.

```powershell
dotnet test CulinaryBlog/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet build CulinaryBlog/CulinaryBlog.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
```

Kết quả xác minh sau sửa lỗi cache và validation: **36/36 test đạt**, gồm integration test PostgreSQL/Redis thật, không skip. Build solution đạt, **0 warning / 0 error**. Script HTTP chạy thành công với **19 trường hợp mã HTTP**, cùng kiểm tra danh mục không tồn tại, tương đương tên/số enum, kết hợp bốn bộ lọc, hồi quy trùng cache key và phân trang.

Chạy lại trên database test riêng theo hạ tầng sẵn có:

```powershell
docker compose up -d postgres redis
$env:RUN_PAGINATION_INTEGRATION='1'
dotnet test CulinaryBlog/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
python CulinaryBlog/tests/verify_recipe_filters_http.py
```

Script HTTP yêu cầu build Debug trước, Python và Docker CLI. Script khởi động API ở môi trường Production trên cổng loopback tạm, tạo database `recipe_filter_http_<uuid>`, chạy migration/seed trong database đó, rồi dừng API và xóa đúng database vừa tạo. Không migrate hoặc sửa database ứng dụng. Redis dùng version ngẫu nhiên theo database test, các key tự hết hạn theo TTL hiện tại.

Đã xác minh HTTP: maxCookTime=-1, minServings=0, difficulty=999 trả 422; categoryId=invalid, minServings=1.5 hoặc 2147483648 trả 400. Request hợp lệ với categoryId không tồn tại trả 200/items rỗng. `difficulty=Easy,Medium` trả 422 thay vì bị Enum.TryParse ghép thành một mức độ khó khác.

## Các vấn đề đã xử lý tiếp

- Khởi động lại PostgreSQL/Redis của Docker Compose để hoàn tất kiểm thử thực tế.
- Đồng bộ tên database development và design-time factory thành `CulinaryBlog`, khớp Docker Compose. Factory hỗ trợ biến môi trường `ConnectionStrings__DefaultConnection` khi dùng database khác.
- Tắt provider Windows EventLog trong ứng dụng trên Windows để lỗi thiếu quyền ghi EventLog không che lỗi database; các provider khác, gồm Console, vẫn hoạt động.
- Bác bỏ chuỗi ghép nhiều mức độ khó, đồng thời giữ tên không phân biệt hoa/thường và số enum 1–4.
- Bổ sung script HTTP có thể chạy lại thay cho chỉ kiểm tra thủ công.
- Thay cache key ghép chuỗi bằng SHA-256 của JSON chứa các tham số truy vấn có kiểu và danh tính người đọc. Định dạng `recipes:list:v2:<hash>:v<generation>` không đọc lại key cũ; key cũ tự hết hạn sau TTL, không cần xóa toàn bộ Redis.
- Chuẩn hóa sort theo tám giá trị đã hỗ trợ; giá trị khác tiếp tục về `-createdAt` như hành vi cũ. Chuẩn hóa trước cả tạo cache key và sắp xếp, tránh nhiều key khác nhau cho cùng cách sắp xếp mặc định.
- Test hồi quy xác nhận request `?sort=foo_cid__max_min_-createdAt` không làm request `?keyword=_cid__max_min_foo&sort=-createdAt` nhận nhầm dữ liệu cache. Kiểm tra cả handler và HTTP thật.
- `ValidationBehavior` của MediatR chịu trách nhiệm validation truy vấn. Bỏ việc handler tự tạo validator và bỏ kiểm tra miền page/pageSize lặp tại endpoint. Endpoint vẫn parse difficulty và chuyển ValidationException thành 422. Lời gọi ứng dụng phải qua MediatR; test gọi handler trực tiếp chỉ dùng truy vấn hợp lệ. Test dữ liệu sai kiểm tra validator/pipeline và xác nhận pipeline không gọi handler.

## Khác biệt kỹ thuật với SRS còn được giữ lại

- Cache danh sách hiện dùng Redis cache-aside với version trong database, không phải policy Output Cache như mô tả FR-RCP-001. Đây là thiết kế hiện có được giữ theo phạm vi triển khai, không coi là đã đáp ứng đúng từng chi tiết kỹ thuật của SRS.
- Model snapshot chưa có index riêng cho Difficulty/Status như bảng mô hình SRS. Chưa bổ sung migration chỉ dựa trên review này; cần đánh giá truy vấn và kế hoạch triển khai index. Kiểm thử hiện tại xác nhận tính đúng đắn, không thay thế kiểm thử hiệu năng.
- Lọc trên endpoint full-text search vẫn thuộc đợt triển khai FR-SRCH-001; giao diện vẫn để làm sau theo yêu cầu.

Chưa nâng cấp migration trên database ứng dụng hiện có trong lần kiểm thử này. Khi chạy ứng dụng development, cần dịch vụ Docker hoạt động và connection đúng; user-secrets/biến môi trường có thể ghi đè cấu hình file. Môi trường Production phải cung cấp connection thật qua cấu hình triển khai; giá trị placeholder trong appsettings.json không phải thông tin đăng nhập dùng được.
