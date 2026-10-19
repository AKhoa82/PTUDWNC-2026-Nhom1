# FR-SRCH-003 — Sắp xếp công thức

Căn cứ: `SRS.md` mục 3.4, FR-RCP-001 và mục 9 của
`SRS_CONFLICT_RESOLUTIONS.md`. Dùng một tham số `sort`, không dùng
`sortBy` / `sortOrder`. Chức năng tích hợp vào `GET /api/v1/recipes`
và trang `/recipes`.

| Trường | Tăng dần | Giảm dần |
| --- | --- | --- |
| Ngày tạo | `createdAt` | `-createdAt` (mặc định) |
| Tên công thức | `title` | `-title` |
| Thời gian nấu | `cookTime` | `-cookTime` |
| Ngày xuất bản (đã có trong dự án) | `publishedAt` | `-publishedAt` |

Ví dụ: `/api/v1/recipes?sort=-cookTime&page=1&pageSize=12`.
`cookTime` là `CookingTimeMinutes`, không bao gồm thời gian chuẩn bị.
Tên được sắp xếp theo collation của cơ sở dữ liệu.

Giữ hành vi tương thích hiện có: thiếu, rỗng hoặc không hỗ trợ `sort`
thì dùng `-createdAt`. Bỏ khoảng trắng đầu/cuối; tên trường phân biệt
hoa thường. Chỉ hỗ trợ một trường mỗi lần, không hỗ trợ danh sách phân
tách bằng dấu phẩy. Giao diện dùng cùng quy tắc mặc định với API.

Lọc và kiểm tra quyền xem trước khi sắp xếp; sắp xếp trước `Skip/Take`.
Khi giá trị bằng nhau, dùng `Id` tăng dần để phân trang ổn định trên dữ
liệu không thay đổi. Cache bao gồm giá trị `sort` đã chuẩn hóa.

Trên giao diện, chọn “Sắp xếp”, rồi nhấn “Tìm kiếm”. Gửi biểu mẫu giữ
các bộ lọc và kích thước trang, trở về trang 1. Các liên kết phân trang
giữ lựa chọn sắp xếp. Lựa chọn hiển thị được cập nhật khi điều hướng
sang URL có thứ tự khác.

## Kiểm tra nghiệm thu

Chuẩn bị ít nhất 4 công thức Published có tên, ngày tạo, thời gian nấu
khác nhau; có ít nhất 2 công thức cùng thời gian nấu. Đặt `pageSize=2`
để kiểm tra qua nhiều trang.

1. Gọi API với từng giá trị trong bảng; ghép kết quả các trang và kiểm
   tra thứ tự tăng/giảm tương ứng. Tổng số kết quả không đổi theo `sort`.
2. Không truyền `sort`, truyền `sort=`, `sort=unknown` và
   `sort=%20-createdAt%20`: kết quả phải giống `sort=-createdAt`.
3. Kết hợp `sort` với `keyword`, `categoryId`, `difficulty`,
   `maxCookTime`, `minServings`: kết quả vẫn thỏa tất cả bộ lọc.
4. Với các công thức cùng giá trị sắp xếp, kiểm tra thứ tự `Id` tăng
   dần, không lặp/mất công thức khi chuyển trang trên dữ liệu cố định.
5. Gọi xen kẽ `sort=cookTime` và `sort=-cookTime` nhiều lần để kiểm tra
   cache không trả nhầm thứ tự.
6. Truy cập `/recipes?sort=unknown`, kiểm tra chọn “Mới nhất”; chuyển
   trang vẫn dùng `sort=-createdAt`. Từ trang 2, chọn “Tên Z–A” và nhấn
   “Tìm kiếm”: trở về trang 1, giữ bộ lọc và kích thước trang.

Endpoint tìm kiếm toàn văn riêng `/api/v1/recipes/search` và trang `/search`
đã được bổ sung trong [FR-SRCH-001](FR-SRCH-001.md), xếp theo độ liên quan.
Tìm kiếm kết hợp sắp xếp tại endpoint danh sách vẫn dùng `keyword`.

## Kiểm thử tự động

- `dotnet test CulinaryBlog/CulinaryBlog.Application.Tests/CulinaryBlog.Application.Tests.csproj`:
  15 ca kiểm thử handler trên EF Core InMemory, gồm các chiều sắp xếp,
  phân trang khi trùng giá trị, chuẩn hóa đầu vào, bộ lọc và cache.
- Trong `CulinaryBlog.FE`, chạy `npm run build`, `npm run start`, rồi
  ở terminal khác chạy `node tests/recipe-sorting-ssr.mjs`:
  11 kịch bản kiểm tra HTML server render, lựa chọn đang hiển thị,
  query gửi API, liên kết phân trang và biểu mẫu trở về trang đầu.
  Test cần cổng 5018 trống và frontend dùng API mặc định
  `http://localhost:5018/api`; API fixture tự đóng khi test kết thúc.

Đã chạy đạt 15 test handler, 11 kịch bản SSR và frontend production
build. Lệnh lint hiện có cũng đạt, nhưng cấu hình lint của dự án chỉ
bao phủ JS/JSX; TypeScript được kiểm tra qua production build.
Chưa xác minh collation/SQL trên PostgreSQL thật, Redis thật hoặc thao
tác trong trình duyệt: dịch vụ cơ sở dữ liệu chưa chạy và công cụ
trình duyệt chưa kết nối trong phiên kiểm tra. Các test trên không
thay thế kiểm thử tích hợp đầy đủ với các dịch vụ này.
