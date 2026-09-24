# Review dự án và FR-SRCH-004

## Phạm vi

Rà soát tĩnh cấu trúc .NET 10/CQRS/EF Core và Next.js, tập trung luồng tìm kiếm, phân trang, quyền đọc và các endpoint liên quan. Đây không phải kiểm toán toàn bộ tính năng hay kiểm thử tải.

## Phát hiện còn tồn tại ngoài phạm vi thay đổi

1. **P1 – Lộ công thức chưa xuất bản qua chi tiết danh mục.** `CulinaryBlog.Application/Features/Categories/Queries/GetCategoryBySlug/GetCategoryBySlugQueryHandler.cs:21` Include toàn bộ Recipes và dòng 35 ánh xạ tất cả trạng thái. Endpoint `CulinaryBlog.API/Program.cs:118` không yêu cầu xác thực, không truyền danh tính người đọc. Guest biết slug danh mục có thể đọc metadata của Draft/Archived. Cần lọc Published/owner/admin trước COUNT và phân trang theo FR-CAT-002.
2. **P1 – Endpoint bước thực hiện tham chiếu policy chưa đăng ký.** `CulinaryBlog.API/Endpoints/RecipeStepEndpoints.cs:12` yêu cầu `VerifiedAuthor`, nhưng `Program.cs:53` chỉ gọi `AddAuthorization()` và không khai báo policy này. Request tới nhóm endpoint sẽ lỗi khi giải quyết policy thay vì thực hiện phân quyền. Cần định nghĩa policy cùng điều kiện tài khoản đã xác minh.
3. **P2 – Trang danh mục gọi sai URL.** `CulinaryBlog.FE/src/app/categories/page.tsx:19` gọi `/api/categories`, trong khi backend chỉ map danh sách tại `/api/v1/categories`. Trang danh mục hiện nhận 404; nên dùng cấu hình API chung và endpoint có version.
4. **P2 – Chưa triển khai FR-SRCH-001.** `GetRecipesQueryHandler` hiện tìm bằng `ToLower().Contains()`; chưa có endpoint `/api/v1/recipes/search`, tsvector, xếp hạng hay tìm không dấu theo SRS. Trang `/recipes` mới sử dụng tìm từ khóa hiện có; chức năng này không thay thế tìm kiếm toàn văn.
5. **P2 – Tạo công thức chưa bắt buộc xác thực.** `Program.cs` map POST `/api/v1/recipes` không có `RequireAuthorization()`, handler lưu `AuthorId` có thể null. Guest có thể tạo Draft không có chủ sở hữu. Cần bắt buộc quyền Author và lấy ID từ claims trước khi ghi dữ liệu.

## FR-SRCH-004 đã triển khai

- Giữ API `GET /api/v1/recipes?keyword=...&page=1&pageSize=12`: mặc định trang 1, kích thước 12, tối đa 50, response `PagedResult` có đủ metadata. Endpoint hiện có trả 422 cho page/pageSize ngoài miền hợp lệ; lỗi bind kiểu dữ liệu vẫn do Minimal API xử lý (400).
- Bổ sung validator Application, ngăn lời gọi MediatR/handler trực tiếp bỏ qua kiểm tra miền giá trị.
- Thêm `ThenBy(Id)` cho mọi kiểu sort để các bản ghi cùng giá trị không bị đổi vị trí tùy ý giữa các trang.
- Tính offset bằng `long`, giới hạn về `int.MaxValue` trước `Skip`: mọi trang vượt phạm vi trả items rỗng và giữ totalCount/page/pageSize chính xác, kể cả `page=2147483647&pageSize=50`.
- Trang công khai `/recipes`: tìm kiếm, bộ lọc, sort, chọn 1–50 kết quả/trang; điều hướng đầu/trước/số trang/sau/cuối; hiển thị phạm vi và tổng số kết quả; trạng thái loading, lỗi, rỗng, trang vượt phạm vi.
- Query string lưu trạng thái tìm kiếm. Link phân trang giữ bộ lọc và pageSize; gửi form đặt lại trang 1. Reload và Back/Forward giữ URL. Có liên kết từ trang đăng ký để truy cập.
- Trang SSR này hiển thị dữ liệu công khai (không chuyển token localStorage lên server). API vẫn giữ phân quyền Guest/Author/Admin hiện có.

## Kiểm thử và chạy thử

```powershell
dotnet test CulinaryBlog/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj
dotnet build CulinaryBlog/CulinaryBlog.sln
cd CulinaryBlog.FE
npm ci
npx tsc --noEmit
npm run build
```

Unit tests sử dụng EF Core InMemory và MemoryDistributedCache, bao phủ thứ tự trùng nhau trên mọi sort, trang đầu/cuối, giới hạn pageSize, offset tràn int, lọc trước đếm, phân biệt cache từng trang và quyền xem dữ liệu. Bộ integration test bên dưới chạy PostgreSQL/Redis thật.

Chạy API với PostgreSQL và Redis theo cấu hình dự án, chạy frontend bằng `npm run dev`, mở `/recipes`. Thử pageSize=1 để kiểm tra nhiều trang với dữ liệu mẫu; đổi từ khóa/bộ lọc rồi chuyển trang và Back; truy cập `?page=2147483647&pageSize=50` để kiểm tra kết quả rỗng. Có thể gọi API trực tiếp với page=0 hoặc pageSize=51 để kiểm tra 422.

Offset pagination bảo đảm thứ tự khi dữ liệu không thay đổi; bản ghi được thêm/xóa giữa hai request vẫn có thể làm dịch chuyển trang.

## Hoàn thiện ngày 24/09/2026

- Cache danh sách dùng generation lưu trong bảng `RecipeListVersions` của PostgreSQL. `ApplicationDbContext` đổi generation trong cùng SaveChanges/transaction với thêm/sửa/xóa Recipe hoặc Category. Hỗ trợ cả lưu đồng bộ, bất đồng bộ và transaction bên ngoài: rollback không đổi generation đã commit; commit công bố dữ liệu và generation cùng nhau.
- Mỗi request công khai đọc generation từ database trước khi dùng Redis. Redis chỉ chứa các trang có TTL 15 phút; request đang chạy với generation cũ không thể ghi đè trang thuộc generation mới. Nếu Redis lỗi GET/SET, handler ghi log và trả dữ liệu database; việc lưu công thức không gọi Redis. Redis phục hồi không làm các trang cũ được dùng lại.
- Nút “Trước” khi URL vượt số trang đưa về trang cuối hợp lệ, giữ nguyên bộ lọc và pageSize.
- Bổ sung test hồi quy cho cache sau thay đổi trạng thái, đổi riêng tên danh mục, xóa công thức, request ghi cache trễ và Redis lỗi/khôi phục.
- Bổ sung test PostgreSQL/Redis thật: tạo database `pagination_test_<guid>`, áp dụng toàn bộ migrations và dùng prefix Redis riêng; xác minh trang đầu/cuối, cache, offset cực lớn, invalidation, rollback và commit transaction bên ngoài. Database thử được xóa khi kết thúc; các page key thử tự hết hạn.

Migration mới `AddRecipeListVersion` phải được áp dụng trước khi chạy API mới:

```powershell
dotnet ef database update --project CulinaryBlog/CulinaryBlog.Infrastructure --startup-project CulinaryBlog/CulinaryBlog.API
```

Design-time factory mặc định trỏ tới database development `culinary_blog`; dùng `--connection` khi triển khai sang database khác. Các instance ghi dữ liệu cần được nâng cấp đồng bộ để đều cập nhật generation.

Tình trạng local đã kiểm tra: Docker hiện có database `CulinaryBlog`, không có `culinary_blog`; connection trong appsettings.json không xác thực được với container hiện tại. Database `CulinaryBlog` còn các migration cũ từ `AddIdentityAndRefreshTokens` trở đi chưa áp dụng. Lần sửa này chỉ chạy migration trên database thử riêng, chưa áp dụng vào database ứng dụng; cần thống nhất connection và rà soát các migration cũ trước khi cập nhật database đó.

Chạy kiểm thử tích hợp (PostgreSQL và Redis phải đang chạy):

```powershell
$env:RUN_PAGINATION_INTEGRATION='1'
dotnet test CulinaryBlog/tests/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
```

Có thể cấu hình `PAGINATION_TEST_POSTGRES` và `PAGINATION_TEST_REDIS`; mặc định dùng dịch vụ localhost trong docker-compose. Khi không bật biến tích hợp, test này được skip.

Đã xác minh: 25 test đạt, bao gồm integration test PostgreSQL/Redis thật và áp dụng migrations từ database trống; build solution đạt, không warning/error. Build production Next.js đã đạt ở lần sửa giao diện trước, lần sửa này không thay đổi frontend. Chưa chạy tự động hóa trình duyệt end-to-end.

Giới hạn: invalidation áp dụng cho thay đổi được lưu qua ApplicationDbContext. SQL trực tiếp/ExecuteUpdate/ExecuteDelete cần tự cập nhật `RecipeListVersions` trong cùng transaction. Mỗi request công khai cần thêm một truy vấn nhỏ để đọc generation; khi Redis không phản hồi phải chờ timeout của Redis client rồi mới fallback. Offset pagination vẫn có thể dịch trang khi dữ liệu thay đổi giữa các request theo đúng đặc tính thiết kế trong SRS.
