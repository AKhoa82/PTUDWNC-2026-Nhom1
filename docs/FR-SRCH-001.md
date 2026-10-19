# FR-SRCH-001 — Tìm kiếm toàn văn

## Review SRS và quyết định triển khai

Đã đối chiếu `SRS.md` mục 3.4, 7.2 và route `/search`, nội dung PDF
`SRS_Culinary_Blog_v1.0.0.pdf` trang 36–37, `SRS_INCONSISTENCIES.md`,
`SRS_CONFLICT_RESOLUTIONS.md` và chức năng hiện có trong `docs/FR-SRCH-003.md`.

- Dùng HTTP 422 cho validation nghiệp vụ, trả trực tiếp `PagedResult`, mặc định
  `page=1`, `pageSize=12`, tối đa 50 theo các quyết định 5, 14, 15.
- SRS vừa gọi SearchVector là computed column vừa yêu cầu trigger. Triển khai
  cột `tsvector` do trigger quản lý theo mục 7.2, không dùng generated expression.
- Tạo cấu hình `public.vietnamese` từ `simple`, dùng từ điển `unaccent` để bỏ dấu.
  Cùng cấu hình được áp dụng cho văn bản và truy vấn. Không yêu cầu cài bộ tách từ
  tiếng Việt bên ngoài. Đây là tìm tiền tố và không dấu; không sửa lỗi chính tả.
- Theo luồng riêng FR-SRCH-001, thứ tự mặc định là `ts_rank DESC`, sau đó `Id ASC`
  để phân trang ổn định. Tiêu đề trọng số A, mô tả trọng số B.
- Endpoint danh sách và trang `/recipes` vẫn giữ lọc/sắp xếp FR-SRCH-003;
  endpoint tìm kiếm riêng dùng `q`, `page`, `pageSize` và thứ hạng độ liên quan.
- Domain hiện chưa có `IsDeleted`; tìm kiếm lọc Published theo mô hình thực tế.
  Việc bổ sung soft delete toàn dự án thuộc chức năng xóa công thức.

Tham khảo kỹ thuật: [PostgreSQL unaccent](https://www.postgresql.org/docs/17/unaccent.html)
và [Npgsql Full Text Search](https://www.npgsql.org/efcore/mapping/full-text-search.html).

## API và giao diện

`GET /api/v1/recipes/search?q=pho+bo&page=1&pageSize=12`

Không cần đăng nhập. Guest, Author, Admin đều chỉ nhận công thức Published.
Tìm trong Title và Description, kết hợp từ bằng AND, mỗi từ khớp tiền tố:
`pho bo` → `pho:* & bo:*`. Chuẩn hóa Unicode NFC và chữ thường, ký tự ngoài
chữ/số được coi là dấu phân cách; truy vấn được EF parameterize.

Response gồm `items`, `totalCount`, `page`, `pageSize`, `totalPages`,
`hasNextPage`, `hasPreviousPage`; mỗi item có `relevanceScore`.
Không tìm thấy: 200, `items=[]`, `message` gợi ý từ khóa khác. Trang vượt phạm vi
trả items rỗng và giữ tổng số kết quả. Không cache kết quả tìm kiếm.

Thiếu/rỗng `q`, từ khóa sau trim ít hơn 2 ký tự, chỉ có dấu câu, `page < 1`
hoặc `pageSize` ngoài 1–50 trả 422 ValidationProblem. Lỗi binding như `page=abc`
trả 400 theo ASP.NET Minimal API.

Trang SSR `/search` có biểu mẫu GET, trạng thái lỗi/rỗng và phân trang giữ từ khóa,
kích thước trang. Gửi tìm kiếm mới trở về trang 1. Có liên kết từ `/recipes`.

## Migration

Migration `20260930122846_AddRecipeFullTextSearch` cài extensions `unaccent`,
`pg_trgm`, thêm cột SearchVector, cấu hình vietnamese, trigger INSERT/UPDATE
Title/Description, backfill dữ liệu cũ và GIN index `IDX_Recipe_Search`.
Rollback xóa đối tượng của chức năng, giữ extensions có thể dùng chung.

Chạy từ thư mục gốc với connection string môi trường đã trỏ đúng database:

```powershell
dotnet ef database update --project CulinaryBlog/CulinaryBlog.Infrastructure --startup-project CulinaryBlog/CulinaryBlog.Infrastructure
```

Tài khoản migration cần quyền cài extensions và tạo cấu hình/function/trigger.
File `FR-SRCH-001-migration.sql` là SQL để review từ migration liền trước;
ưu tiên dùng EF CLI để quản lý lịch sử migration. Migration backfill và tạo
index trong transaction có thể giữ khóa khi bảng Recipes lớn.

## Kiểm thử

```powershell
dotnet test CulinaryBlog/CulinaryBlog.Application.Tests -m:1 -p:UseSharedCompilation=false
```

16 ca mới kiểm tra chuẩn hóa Unicode, tiền tố, ký tự đặc biệt, validation.
15 ca sắp xếp hiện có vẫn đạt. Test PostgreSQL được bỏ qua nếu chưa đặt
`SEARCH_TEST_POSTGRES`; khi đặt, test tạo database tên ngẫu nhiên, chạy migrations,
kiểm tra backfill/trigger, không dấu (kể cả đ), tiền tố, AND, ranking, Published,
phân trang, đồng hạng, kết quả rỗng, injection và rollback/reapply rồi xóa database
test trong finally. Connection cần quyền CREATE DATABASE.

```powershell
$env:SEARCH_TEST_POSTGRES = 'Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres'
dotnet test CulinaryBlog/CulinaryBlog.Application.Tests -m:1 -p:UseSharedCompilation=false
```

Trong `CulinaryBlog.FE`: `npm run build`, `npm run start`, rồi terminal khác:

```powershell
node tests/recipe-search-ssr.mjs
node tests/recipe-sorting-ssr.mjs
```

Fixture cần port 5018 trống, Next.js ở 3000 và API URL mặc định. Kiểm tra SSR
gồm 9 kịch bản tìm kiếm và 11 kịch bản sắp xếp cũ; không thay thế thao tác trình duyệt.

Đã xác minh backend build, frontend production build, model/migration đồng bộ,
32 test .NET trên PostgreSQL 18.6 tạm riêng và 20 kịch bản SSR. SRS nêu PostgreSQL
16.x; chưa chạy lại trên phiên bản 16. Chưa áp dụng migration vào database dự án.
