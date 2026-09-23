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

Tests sử dụng EF Core InMemory và MemoryDistributedCache thay thế dịch vụ ngoài, bao phủ thứ tự trùng nhau trên mọi sort, trang đầu/cuối, giới hạn pageSize, offset tràn int, lọc trước đếm, phân biệt cache từng trang và quyền xem dữ liệu. Không thay thế kiểm thử SQL trên PostgreSQL/Redis thật.

Chạy API với PostgreSQL và Redis theo cấu hình dự án, chạy frontend bằng `npm run dev`, mở `/recipes`. Thử pageSize=1 để kiểm tra nhiều trang với dữ liệu mẫu; đổi từ khóa/bộ lọc rồi chuyển trang và Back; truy cập `?page=2147483647&pageSize=50` để kiểm tra kết quả rỗng. Có thể gọi API trực tiếp với page=0 hoặc pageSize=51 để kiểm tra 422.

Offset pagination bảo đảm thứ tự khi dữ liệu không thay đổi; bản ghi được thêm/xóa giữa hai request vẫn có thể làm dịch chuyển trang. Không có migration/schema mới.
