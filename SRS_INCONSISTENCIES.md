# BÁO CÁO PHÂN TÍCH CÁC ĐIỂM MÂU THUẪN VÀ BẤT NHẤT TRONG TÀI LIỆU SRS (v1.0.0)

> **Dự án:** Blog Ẩm thực và Nấu ăn (Culinary Blog)  
> **Tài liệu đối chiếu:** [SRS.md](file:///d:/PTUDWNC-2026-Nhom1/SRS.md)  
> **Ngày lập báo cáo:** 16/09/2026 (Phiên bản rà soát toàn diện lần cuối)  
> **Mục đích:** Rà soát tính nhất quán nội bộ (Internal Consistency) giữa các chương trong tài liệu SRS v1.0.0 trước khi triển khai phát triển.

---

## 📑 BẢNG TỔNG HỢP MỨC ĐỘ ẢNH HƯỞNG (SEVERITY MATRIX)

Sau khi rà soát kỹ lưỡng toàn bộ 8 chương và 3 phụ lục của tài liệu SRS, đã phát hiện tổng cộng **18 điểm mâu thuẫn và bất nhất nội bộ**:

| STT | Vấn đề mâu thuẫn | Vị trí đối chiếu trong SRS | Mức độ nghiêm trọng |
| :---: | :--- | :--- | :---: |
| **01** | **Xóa Recipe: Hard Delete hay Soft Delete?** | FR-RCP-007 vs NFR-REL-003, Mục 7.1/7.2, API 8.3, Phụ lục A | 🔴 **CRITICAL** |
| **02** | **Xóa Category: Hard Delete hay Soft Delete?** | FR-CAT-005 vs Mục 7.6, API 8.2 | 🔴 **CRITICAL** |
| **03** | **Điều kiện Xuất bản Công thức (Publish Recipe)** | FR-RCP-005 vs Phụ lục A & B | 🔴 **CRITICAL** |
| **04** | **Step Number & Title trong Các bước nấu (RecipeStep)** | FR-RCP-010 vs Mục 7.3, API 8.5 | 🔴 **CRITICAL** |
| **05** | **Mã HTTP cho lỗi Validation: 422 hay 400?** | Toàn bộ Chương 3 vs Chương 8, Phụ lục A & B | 🔴 **CRITICAL** |
| **06** | **Mã HTTP cho lỗi xung đột Concurrency: 409 hay 422?** | FR-RCP-004 vs Phụ lục A, Phụ lục B | 🔴 **CRITICAL** |
| **07** | **Số lượng & Đơn vị Nguyên liệu (Quantity vs Unit)** | FR-RCP-009 vs Mục 7.4 | 🟠 **HIGH** |
| **08** | **Cấu trúc trường Dinh dưỡng (Nutrition fields)** | FR-RCP-003/004 vs Mục 7.2.1 | 🟠 **HIGH** |
| **09** | **Tham số Query Sắp xếp (`sort` vs `sortBy & sortOrder`)** | FR-RCP-001, FR-SRCH-003 vs API 8.0, 8.3 | 🟠 **HIGH** |
| **10** | **Chiến lược Caching Danh mục: IMemoryCache vs Redis Cache & TTL** | FR-CAT-001 vs NFR-PERF-003, NFR-SCALE-001, Mục 6.3 | 🟠 **HIGH** |
| **11** | **Thời gian sống & Cơ chế Cache Recipe Detail: 60p vs 5p** | FR-RCP-002 vs NFR-PERF-003 | 🟠 **HIGH** |
| **12** | **Xử lý trùng Title / Slug: Báo lỗi 409 hay Tự sinh Suffix số?** | FR-RCP-003 vs Mục 7.2, Phụ lục B | 🟠 **HIGH** |
| **13** | **Bản chất thực thể `RecipeImage` (Owned hay Bảng riêng?)** | Bảng 6.4 (ERD) vs Mục 7.5, API 8.4 | 🟠 **HIGH** |
| **14** | **Cấu trúc chuẩn Response API (Envelope Payload)** | Mục 5.2, API 8.0 vs Các Use Case Chương 3 | 🟠 **HIGH** |
| **15** | **Kích thước trang mặc định (Default PageSize): 12 hay 10?** | FR-RCP-001, FR-SRCH-004 vs API 8.0, 8.2 | 🟡 **MEDIUM** |
| **16** | **Trường dữ liệu Người dùng (`Username` vs `DisplayName`)** | FR-AUTH-001 vs API 8.1, Mục 7.7 | 🟡 **MEDIUM** |
| **17** | **Ràng buộc thời gian nấu (`CookTime > 0` vs `CookTime >= 0`)** | FR-RCP-003 vs Mục 7.2 | 🟡 **MEDIUM** |
| **18** | **Kích thước Token & Phương thức truyền Refresh Token** | FR-AUTH-001, Mục 5.2 vs NFR-SEC-002, NFR-SEC-005 | 🟢 **LOW** |

---

## 🔍 CHI TIẾT 18 ĐIỂM MÂU THUẪN NỘI BỘ TRONG SRS

### 1. Cơ chế Xóa Công thức (Recipe): Hard Delete hay Soft Delete?
* **Chương 3 (FR-RCP-007, Trang 32–33):** Quy định **Hard Delete**: *"Xóa vĩnh viễn một công thức và tất cả dữ liệu liên quan (cascade delete: Steps, Ingredients, Images)... Đây là hard delete (không dùng soft delete pattern cho recipe)"*, xóa file MinIO qua Hangfire.
* **Chương 4 (NFR-REL-003, Trang 43), Mục 7.1/7.2 (Trang 54, 56), API 8.3 (Trang 64):** Quy định dùng **Soft Delete**: *"Recipe được đánh dấu `IsDeleted` thay vì xóa vật lý (có thể khôi phục)"*.

### 2. Cơ chế Xóa Danh mục (Category): Hard Delete hay Soft Delete?
* **Chương 3 (FR-CAT-005, Trang 26–27):** Kiểm tra không còn recipe trong danh mục thì thực hiện `_context.Categories.Remove(entity)` $\rightarrow$ **Hard Delete**.
* **Chương 7 (Mô hình Dữ liệu 7.6, Trang 58) & Chương 8 (API 8.2, Trang 63):** Category kế thừa `BaseEntity` (có `IsDeleted`) và API 8.2 mô tả: *"Xóa category (soft delete)"*.

### 3. Điều kiện Xuất bản Công thức (Publish Recipe)
* **Chương 3 (FR-RCP-005, Trang 31):** Chỉ kiểm tra `Steps.Count > 0`. Nếu `Steps.Count == 0` thì throw `DomainException`. Hoàn toàn không kiểm tra `Ingredients`.
* **Phụ lục B (Mã lỗi `RECIPE_PUBLISH_INCOMPLETE`, Trang 68) & Phụ lục A (Trang 67):** Bắt buộc phải có cả: **ít nhất 1 ingredient và 1 step**.

### 4. Step Number & Title trong Các bước nấu (RecipeStep)
* **Chương 3 (FR-RCP-010, Trang 35–36):** Client gửi `{ description, durationMinutes?, imageUrl? }` (không gửi `stepNumber`, không có `title`). Backend tự tính `StepNumber = Max + 1` và tự renumber liên tục.
* **Chương 7 (Mục 7.3, Trang 57) & Chương 8 (API 8.5, Trang 65):** Bắt buộc có `Title varchar(200) NOT NULL`. API bắt Client tự truyền `stepNumber` và cho phép sửa `stepNumber`.

### 5. Mã HTTP Status cho Lỗi Validation Dữ liệu: 422 hay 400?
* **Chương 3 (Toàn bộ các FR từ FR-AUTH-001 đến FR-SRCH-001):**
  Quy định nhất quán: Validation thất bại $\rightarrow$ Trả về **HTTP 422 Unprocessable Entity** (theo RFC 7807 ValidationProblemDetails).
* **Chương 8 (API Spec 8.1, 8.2, 8.3) & Phụ lục A, B (Trang 61, 67, 69):**
  Lại quy định: Validation thất bại $\rightarrow$ Trả về **HTTP 400 Bad Request** (`VALIDATION_ERROR`).

### 6. Mã HTTP Status cho Xung đột Concurrency (RowVersion mismatch): 409 hay 422?
* **Chương 3 (FR-RCP-004, Trang 30, 31):**
  Dòng 9 và A2: Khi phát hiện `DbUpdateConcurrencyException` do lệch RowVersion $\rightarrow$ Trả về **HTTP 409 Conflict** (*"Dữ liệu đã bị thay đổi bởi người dùng khác"*).
* **Phụ lục A (Trang 67) & Phụ lục B (Trang 68):**
  Lại xếp lỗi `RowVersion conflict — Optimistic Concurrency` vào mã lỗi `RECIPE_CONCURRENCY_CONFLICT` với **HTTP 422 Unprocessable Entity**.

### 7. Số lượng (Quantity) & Đơn vị (Unit) của Nguyên liệu
* **Chương 3 (FR-RCP-009, Trang 35):** Ràng buộc `Quantity > 0`, `Unit không rỗng`.
* **Chương 7 (Mô hình Dữ liệu 7.4, Trang 57):** Cột `Quantity decimal(10,3) NULL` (*"Nullable cho 'nguyên liệu vừa đủ'"*). Cột `Unit varchar(50) NULL`.

### 8. Cấu trúc trường Dinh dưỡng (Nutrition)
* **Chương 3 (FR-RCP-003 & FR-RCP-004, Trang 29, 30):** Nhận 4 trường: `calories, protein, carbs, fat`.
* **Chương 7 (Mô hình Dữ liệu 7.2.1, Trang 56):** Bảng có **6 trường**: `Calories`, `Protein`, `Carbohydrates`, `Fat`, `Fiber` (Chất xơ), `Sodium` (Natri).

### 9. Tham số Query Sắp xếp (`sort` vs `sortBy & sortOrder`)
* **Chương 3 (FR-RCP-001 & FR-SRCH-003, Trang 28, 37):** Dùng 1 tham số: `sort={field}` với tiền tố `"-"` giảm dần (VD: `sort=title`, `sort=-createdAt`).
* **Chương 8 (Quy ước REST API 8.0 & 8.3, Trang 61, 63):** Dùng 2 tham số rời: `sortBy=createdAt&sortOrder=desc`.

### 10. Chiến lược Caching Danh mục: In-Memory vs Redis & TTL
* **Chương 3 (FR-CAT-001, Trang 23–24):** Dùng `IMemoryCache` (TTL 60 phút).
* **Chương 4 (NFR-PERF-003 & NFR-SCALE-001, Trang 40, 44):** Bắt buộc dùng Redis Distributed Cache (TTL 30 phút), cấm dùng `IMemoryCache`.

### 11. Thời gian sống & Cơ chế Cache Recipe Detail: 60 phút vs 5 phút
* **Chương 3 (FR-RCP-002, Trang 28):**
  Cache bằng **Output Cache** theo policy `"RecipeDetail"`, thời gian **TTL = 60 phút** kèm tag `"recipes"`.
* **Chương 4 (NFR-PERF-003, Trang 40):**
  Cache bằng mô hình **Cache-aside** (Redis), thời gian **TTL = 5 phút**.

### 12. Xử lý Trùng Title / Slug: Báo lỗi 409 hay Tự động Thêm Suffix Số?
* **Chương 3 (FR-RCP-003 dòng A3, Trang 30):**
  Ghi nhận: `A3 – Slug đã tồn tại (title trùng): HTTP 409 Conflict`.
* **Chương 7 (Mô hình 7.2, Trang 54) & Phụ lục B (Trang 68):**
  Mục 7.2: *"Title: Unique không bắt buộc (có thể trùng title khác nhau slug)"*.  
  Phụ lục B (`RECIPE_SLUG_EXISTS`): *"Slug đã tồn tại — tự động thêm suffix (slug-1, slug-2...)"*.

### 13. Bản chất thực thể `RecipeImage` (Owned Type hay Bảng riêng?)
* **Chương 6 (Bảng 6.4, Trang 52):** Ghi là `"RecipeImages" (owned — cột trong Recipes)`.
* **Chương 7 (Mục 7.5, Trang 58) & API 8.4 (Trang 64):** Định nghĩa là bảng riêng `RecipeImages` quan hệ 1:N cascade delete.

### 14. Cấu trúc Chuẩn Response API (Envelope Payload)
* **Chương 5 (Mục 5.2) & Chương 8 (Trang 47, 61):** Bắt buộc bọc Envelope: `{ "data": {...}, "meta": {...} }`.
* **Chương 3 (Use Cases):** Trả về DTO phẳng trực tiếp hoặc cấu trúc `PagedResult<T>` không có vỏ bọc `data`.

### 15. Kích thước trang mặc định (Default PageSize): 12 hay 10?
* **Chương 3 (FR-RCP-001 & FR-SRCH-004, Trang 28, 37):**
  Giá trị mặc định là: `pageSize = 12` (phù hợp với lưới giao diện UI chia 2, 3, 4 cột).
* **Chương 8 (Quy ước REST API 8.0 & 8.2, Trang 61, 62):**
  Giá trị mặc định là: `pageSize = 10`.

### 16. Bất nhất Tên thuộc tính User (`Username` vs `DisplayName` vs `FullName`)
* **Chương 3 (FR-AUTH-001, Trang 17):** Body đăng ký: `{ fullName, email, userName, password }`.
* **Chương 8 (Mục 8.1, Trang 61):** Body đăng ký: `{ email, password, displayName }`.

### 17. Ràng buộc Điều kiện `CookTime` (Thời gian nấu)
* **Chương 3 (FR-RCP-003, Trang 29):** Validation bắt buộc `cookTime > 0`.
* **Chương 7 (Mô hình Dữ liệu 7.2, Trang 55):** Ràng buộc SQL: `CookTime integer NOT NULL, CHECK >= 0`.

### 18. Kích thước và Cơ chế Truyền Refresh Token
* **Chương 3 (FR-AUTH-001) & Mục 5.2 (Trang 18, 47):** Token 512-bit, truyền qua Request Body.
* **Chương 4 (NFR-SEC-002 & NFR-SEC-005, Trang 41):** Token 128-bit, truyền qua HTTP-Only Cookie.
