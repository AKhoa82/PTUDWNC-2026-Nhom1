# PHÂN TÍCH PHƯƠNG ÁN GIẢI QUYẾT MÂU THUẪN SRS (v1.0.0)
## Đánh giá Chi tiết Ưu – Nhược điểm & Tác động Kỹ thuật Dựa trên Tài liệu SRS

> **Dự án:** Blog Ẩm thực và Nấu ăn (Culinary Blog)  
> **Tài liệu tham chiếu:** [SRS.md](file:///d:/PTUDWNC-2026-Nhom1/SRS.md) & [SRS_INCONSISTENCIES.md](file:///d:/PTUDWNC-2026-Nhom1/SRS_INCONSISTENCIES.md)  
> **Ngày lập:** 16/09/2026 (Phiên bản hoàn chỉnh sau rà soát toàn diện)  
> **Nguyên tắc phân tích:** Chỉ so sánh và đánh giá dựa trên chính các phương án mâu thuẫn tồn tại trong tài liệu SRS v1.0.0, không đưa ra giả định ngoài phạm vi đặc tả.

---

## 📑 DANH MỤC 18 VẤN ĐỀ MÂU THUẪN

1. [Xóa Recipe: Hard Delete (FR-RCP-007) vs Soft Delete (NFR-REL-003, Mục 7.1)](#1-xóa-recipe-hard-delete-vs-soft-delete)
2. [Xóa Category: Hard Delete (FR-CAT-005) vs Soft Delete (API 8.2, Mục 7.6)](#2-xóa-category-hard-delete-vs-soft-delete)
3. [Điều kiện Publish Recipe: Chỉ cần Steps (FR-RCP-005) vs Bắt buộc cả Steps & Ingredients (Phụ lục A & B)](#3-điều-kiện-publish-recipe-chỉ-cần-steps-vs-bắt-buộc-cả-steps--ingredients)
4. [Bước nấu (RecipeStep): Tự động sinh `StepNumber` (FR-RCP-010) vs Client truyền `StepNumber & Title` (API 8.5)](#4-bước-nấu-recipestep-tự-động-sinh-stepnumber-vs-client-truyền-stepnumber--title)
5. [Mã HTTP cho Lỗi Validation: HTTP 422 (Chương 3) vs HTTP 400 (Chương 8, Phụ lục A & B)](#5-mã-http-cho-lỗi-validation-http-422-vs-http-400)
6. [Mã HTTP cho Xung đột Concurrency: HTTP 409 (FR-RCP-004) vs HTTP 422 (Phụ lục A & B)](#6-mã-http-cho-xung-đột-concurrency-http-409-vs-http-422)
7. [Nguyên liệu: Bắt buộc `Quantity > 0` (FR-RCP-009) vs `Quantity Nullable` (Mục 7.4)](#7-nguyên-liệu-bắt-buộc-quantity--0-vs-quantity-nullable)
8. [Dinh dưỡng: 4 trường (FR-RCP-003) vs 6 trường (Mục 7.2.1)](#8-dinh-dưỡng-4-trường-vs-6-trường)
9. [Tham số Sắp xếp: `sort={field}` (FR-SRCH-003) vs `sortBy & sortOrder` (API 8.0)](#9-tham-số-sắp-xếp-sortfield-vs-sortby--sortorder)
10. [Cache Danh mục: IMemoryCache (FR-CAT-001) vs Redis Cache (NFR-PERF-003, NFR-SCALE-001)](#10-cache-danh-mục-imemorycache-vs-redis-cache)
11. [Cache Recipe Detail: Output Cache TTL = 60 phút (FR-RCP-002) vs Cache-aside TTL = 5 phút (NFR-PERF-003)](#11-cache-recipe-detail-output-cache-ttl--60-phút-vs-cache-aside-ttl--5-phút)
12. [Xử lý Trùng Title/Slug: Báo lỗi 409 Conflict (FR-RCP-003) vs Tự động Thêm Suffix Số (Mục 7.2, Phụ lục B)](#12-xử-lý-trùng-titleslug-báo-lỗi-409-conflict-vs-tự-động-thêm-suffix-số)
13. [RecipeImage: Cột Owned Entity (Bảng 6.4) vs Bảng riêng 1:N (Mục 7.5)](#13-recipeimage-cột-owned-entity-vs-bảng-riêng-1n)
14. [Chuẩn Response API: Envelope `{ data, meta }` (Mục 5.2) vs DTO phẳng (Chương 3)](#14-chuẩn-response-api-envelope--data-meta--vs-dto-phẳng)
15. [Kích thước Trang mặc định (Default PageSize): 12 (Chương 3) vs 10 (Chương 8)](#15-kích-thước-trang-mặc-định-default-pagesize-12-vs-10)
16. [Thuộc tính User khi Đăng ký: `{ fullName, userName }` (FR-AUTH-001) vs `{ displayName }` (API 8.1)](#16-thuộc-tính-user-khi-đăng-ký-fullname-username-vs-displayname)
17. [Ràng buộc `CookTime`: `CookTime > 0` (FR-RCP-003) vs `CookTime >= 0` (Mục 7.2)](#17-ràng-buộc-cooktime-cooktime--0-vs-cooktime--0)
18. [Refresh Token: 512-bit qua Body (FR-AUTH-001, Mục 5.2) vs 128-bit qua Cookie (NFR-SEC-002/005)](#18-refresh-token-512-bit-qua-body-vs-128-bit-qua-cookie)

---

## 1. XÓA RECIPE: HARD DELETE VS SOFT DELETE

### 📌 Mâu thuẫn trong SRS
* **FR-RCP-007 (Trang 32–33):** *"Xóa vĩnh viễn một công thức và tất cả dữ liệu liên quan (cascade delete: Steps, Ingredients, Images)... Đây là hard delete (không dùng soft delete pattern cho recipe)"*, xóa ảnh MinIO qua Hangfire.
* **NFR-REL-003 (Trang 43), Mục 7.1/7.2 (Trang 54, 56), API 8.3 (Trang 64):** *"Recipe được đánh dấu `IsDeleted` thay vì xóa vật lý (có thể khôi phục)"*.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-RCP-007 - Hard Delete):**
  * *Cách thực hiện:* `_unitOfWork.Recipes.Remove(recipe)`, gọi Hangfire xóa ảnh MinIO.
  * *Ưu điểm:* Giải phóng dung lượng DB và MinIO tức thì, không để lại dữ liệu rác.
  * *Nhược điểm:* Dữ liệu mất vĩnh viễn, không thể khôi phục; vi phạm NFR-REL-003.
  * *Ảnh hưởng:* Phải bỏ cột `IsDeleted` trên bảng `Recipes` và bỏ Global Query Filter.
* **Phương án B (Theo NFR-REL-003 & Mục 7.1/7.2 - Soft Delete):**
  * *Cách thực hiện:* Cập nhật `recipe.IsDeleted = true; recipe.UpdatedAt = DateTime.UtcNow;`. Giữ nguyên ảnh MinIO.
  * *Ưu điểm:* Khôi phục 100% dữ liệu nếu lỡ tay bấm nhầm, tận dụng Global Query Filter EF Core.
  * *Nhược điểm:* Tốn dung lượng lưu trữ theo thời gian.
  * *Ảnh hưởng:* Không enqueue xóa MinIO khi Author xóa recipe; sitemap và search phải luôn lọc `!IsDeleted`.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án B (Soft Delete)** cho hành động của người dùng; Admin có thể dọn dẹp vĩnh viễn qua Job định kỳ.

---

## 2. XÓA CATEGORY: HARD DELETE VS SOFT DELETE

### 📌 Mâu thuẫn trong SRS
* **FR-CAT-005 (Trang 26–27):** Kiểm tra `count(recipes) == 0` rồi gọi `Remove(category)` $\rightarrow$ **Hard Delete**.
* **API Spec 8.2 (Trang 63) & Mục 7.6 (Trang 58):** Ghi là *"Xóa category (soft delete)"*, và Category kế thừa `BaseEntity` có `IsDeleted`.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-CAT-005 - Hard Delete):**
  * *Cách thực hiện:* Khi không còn recipe nào liên kết, gọi `_context.Categories.Remove(category)`.
  * *Ưu điểm:* Tránh hoàn toàn lỗi đụng độ `UNIQUE constraint` trên `Name` và `Slug` nếu Admin muốn tạo lại danh mục cùng tên sau này.
  * *Nhược điểm:* Mất bản ghi danh mục khỏi DB.
  * *Ảnh hưởng:* Đính chính mô tả ở API 8.2 thành Hard Delete.
* **Phương án B (Theo API 8.2 & Mục 7.6 - Soft Delete):**
  * *Cách thực hiện:* Gán `category.IsDeleted = true`.
  * *Ưu điểm:* Lưu vết lịch sử danh mục cũ.
  * *Nhược điểm:* Xung đột UNIQUE constraint khi tạo lại danh mục trùng tên/slug đã xóa mềm.
  * *Ảnh hưởng:* Phải tạo Filtered Unique Index trong PostgreSQL.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án A (Hard Delete)** vì danh mục đã rỗng không có giá trị cần lưu vết phục hồi.

---

## 3. ĐIỀU KIỆN PUBLISH RECIPE: CHỈ CẦN STEPS VS BẮT BUỘC CẢ STEPS & INGREDIENTS

### 📌 Mâu thuẫn trong SRS
* **FR-RCP-005 (Trang 31):** Chỉ kiểm tra `Steps.Count > 0` $\rightarrow$ *"KHÔNG thể publish nếu recipe không có ít nhất 1 bước thực hiện"*.
* **Phụ lục A & B (Trang 67, 68):** Lỗi `RECIPE_PUBLISH_INCOMPLETE` bắt buộc phải có cả: **ít nhất 1 ingredient và 1 step**.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-RCP-005):**
  * *Ưu điểm:* Cho phép xuất bản nhanh các mẹo nấu ăn không cần nguyên liệu.
  * *Nhược điểm:* Vi phạm chuẩn SEO Google Recipe Markup (Google yêu cầu trường `recipeIngredient` là bắt buộc để hiển thị Rich Snippets).
  * *Ảnh hưởng:* Phải xóa mã lỗi `RECIPE_PUBLISH_INCOMPLETE` khỏi Phụ lục B.
* **Phương án B (Theo Phụ lục A & B):**
  * *Ưu điểm:* Đảm bảo chuẩn nội dung ẩm thực hoàn chỉnh; đạt 100% chuẩn Google Rich Results Test (NFR-SEO-001).
  * *Nhược điểm:* Người dùng bắt buộc phải nhập tối thiểu 1 nguyên liệu và 1 bước.
  * *Ảnh hưởng:* Thêm kiểm tra `!Ingredients.Any()` vào method `recipe.Publish()` ở Domain Layer.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án B (Cả Steps và Ingredients)** để đạt chuẩn SEO và chất lượng bài viết.

---

## 4. BƯỚC NẤU (RECIPESTEP): TỰ ĐỘNG SINH `StepNumber` VS CLIENT TRUYỀN `StepNumber & Title`

### 📌 Mâu thuẫn trong SRS
* **FR-RCP-010 (Trang 35–36):** Client không gửi `stepNumber`, không có `Title`. Backend tự tính `StepNumber = Max + 1`, tự renumber khi xóa bước.
* **Mục 7.3 & API Spec 8.5 (Trang 57, 65):** Bắt Client tự gửi `stepNumber` và `title` (với `Title varchar(200) NOT NULL`).

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-RCP-010 - Backend tự quản lý):**
  * *Ưu điểm:* Số bước luôn liên tục $1, 2, 3...$, không bao giờ bị trùng lặp hoặc ngắt quãng; chống lỗi `UNIQUE (RecipeId, StepNumber)`.
  * *Nhược điểm:* Thiếu trường `Title` để đặt tên tóm tắt cho từng bước (sơ chế, ướp, xào...).
  * *Ảnh hưởng:* Bỏ cột `Title` trong schema DB.
* **Phương án B (Theo Mục 7.3 & API 8.5 - Client tự truyền):**
  * *Ưu điểm:* Cho phép client tùy biến số bước và đặt tên tiêu đề rõ ràng.
  * *Nhược điểm:* Rất dễ phát sinh lỗi xung đột Unique constraint khi 2 bước trùng số thứ tự.
  * *Ảnh hưởng:* Phải viết thêm validation phức tạp kiểm tra trùng số thứ tự bước.
* **👉 Đề xuất lựa chọn:** **Kết hợp chuẩn hóa:** `StepNumber` do **Backend tự động sinh liên tục**; trường `Title` chuyển thành **Tùy chọn (Nullable)** (nếu Client không gửi thì Backend tự gán `$"Bước {stepNumber}"`).

---

## 5. MÃ HTTP CHO LỖI VALIDATION: HTTP 422 VS HTTP 400

### 📌 Mâu thuẫn trong SRS
* **Chương 3 (Toàn bộ các FR từ FR-AUTH-001 đến FR-SRCH-001):** Quy định khi dữ liệu không hợp lệ trả về **HTTP 422 Unprocessable Entity** (theo RFC 7807).
* **Chương 8 (API Spec 8.1 - 8.3) & Phụ lục A, B (Trang 61, 67, 69):** Quy định validation thất bại trả về **HTTP 400 Bad Request** (`VALIDATION_ERROR`).

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo Chương 3 - HTTP 422 Unprocessable Entity):**
  * *Ưu điểm:* Chuẩn ngữ nghĩa RESTful hiện đại: Cú pháp JSON đúng (`200/OK` format) nhưng giá trị nghiệp vụ không hợp lệ (mật khẩu thiếu hoa/số, tên quá ngắn).
  * *Nhược điểm:* Khác biệt với mã HTTP 400 mặc định của một số thư viện client cũ.
  * *Ảnh hưởng:* Sửa toàn bộ mã 400 ở Chương 8 và Phụ lục A/B thành 422.
* **Phương án B (Theo Chương 8 & Phụ lục A/B - HTTP 400 Bad Request):**
  * *Ưu điểm:* Phổ biến, trực quan, là mã lỗi mặc định của ASP.NET Core `ModelState` và Axios.
  * *Nhược điểm:* Làm mờ ranh giới giữa "lỗi cú pháp JSON hỏng" (Malformed JSON) và "lỗi nghiệp vụ trường dữ liệu".
  * *Ảnh hưởng:* Sửa các use case ở Chương 3 từ 422 thành 400.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án A (HTTP 422)** cho lỗi FluentValidation nghiệp vụ theo đúng chuẩn RFC 7807 đã nêu trong Chương 3; giữ HTTP 400 cho lỗi cú pháp JSON hỏng.

---

## 6. MÃ HTTP CHO XUNG ĐỘT CONCURRENCY: HTTP 409 VS HTTP 422

### 📌 Mâu thuẫn trong SRS
* **FR-RCP-004 (Trang 30, 31):** Khi phát hiện lệch RowVersion ném `DbUpdateConcurrencyException` $\rightarrow$ Trả về **HTTP 409 Conflict**.
* **Phụ lục A (Trang 67) & Phụ lục B (`RECIPE_CONCURRENCY_CONFLICT`, Trang 68):** Lại quy định lỗi RowVersion conflict trả về **HTTP 422 Unprocessable Entity**.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-RCP-004 - HTTP 409 Conflict):**
  * *Ưu điểm:* Chuẩn RFC 7231 / RFC 9110 quy định rõ ràng: Xung đột tài nguyên do đồng thời sửa đổi (Edit Conflict / ETag Mismatch) **bắt buộc phải dùng HTTP 409 Conflict**.
  * *Nhược điểm:* Không khớp với bảng mã lỗi Phụ lục B.
  * *Ảnh hưởng:* Sửa mã lỗi `RECIPE_CONCURRENCY_CONFLICT` trong Phụ lục B thành 409.
* **Phương án B (Theo Phụ lục A & B - HTTP 422 Unprocessable Entity):**
  * *Ưu điểm:* Khớp với bảng Phụ lục B.
  * *Nhược điểm:* Sai chuẩn ngữ nghĩa HTTP (422 là lỗi ngữ nghĩa dữ liệu, không phải lỗi xung đột tài nguyên).
* **👉 Đề xuất lựa chọn:** **Chọn Phương án A (HTTP 409 Conflict)** để đúng chuẩn RFC 7231 và đúng mô tả tại FR-RCP-004.

---

## 7. NGUYÊN LIỆU: BẮT BUỘC `Quantity > 0` VS `Quantity Nullable`

### 📌 Mâu thuẫn trong SRS
* **FR-RCP-009 (Trang 35):** Ràng buộc: `Quantity > 0`, `Unit không rỗng`.
* **Mô hình Dữ liệu 7.4 (Trang 57):** Cột `Quantity decimal(10,3) NULL` (*"Nullable cho 'nguyên liệu vừa đủ'"*). Cột `Unit varchar(50) NULL`.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-RCP-009):** Bắt buộc mọi nguyên liệu phải có số lượng và đơn vị.
  * *Nhược điểm:* Người dùng không thể nhập các gia vị thông dụng kiểu "vừa đủ", "một ít", "tùy khẩu vị".
* **Phương án B (Theo Mô hình 7.4):** Cho phép `Quantity` và `Unit` là Nullable.
  * *Ưu điểm:* Đúng thực tế ẩm thực, đồng thời giữ kiểu số `decimal` để tính toán nhân khẩu phần ăn.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án B (Nullable)**.

---

## 8. DINH DƯỠNG: 4 TRƯỜNG VS 6 TRƯỜNG

### 📌 Mâu thuẫn trong SRS
* **FR-RCP-003/004 (Trang 29, 30):** Chỉ nhận 4 trường: Calo, Đạm, Tinh bột, Chất béo.
* **Mô hình 7.2.1 (Trang 56):** Bảng DB có 6 trường (thêm Chất xơ `Fiber` và Natri `Sodium`).

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-RCP-003/004):** Chỉ dùng 4 trường $\rightarrow$ Bỏ phí 2 cột `Fiber` và `Sodium` trong DB.
* **Phương án B (Theo Mô hình 7.2.1):** Dùng đủ 6 trường dạng Nullable $\rightarrow$ Tối ưu điểm số SEO Google Recipe Rich Snippets.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án B (Đầy đủ 6 trường)**.

---

## 9. THAM SỐ SẮP XẾP: `sort={field}` VS `sortBy & sortOrder`

### 📌 Mâu thuẫn trong SRS
* **FR-RCP-001 & FR-SRCH-003 (Trang 28, 37):** Dùng 1 tham số: `sort={field}` (tiền tố `"-"` giảm dần).
* **Quy ước API 8.0 & 8.3 (Trang 61, 63):** Dùng 2 tham số: `sortBy=createdAt&sortOrder=desc`.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo Chương 3 - `sort={field}`):** Ngắn gọn, chuẩn REST API hiện đại, dễ sắp xếp đa cột (`?sort=-createdAt,title`).
* **Phương án B (Theo Chương 8 - `sortBy & sortOrder`):** Dài dòng hơn trên URL query string, khó sort nhiều cột.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án A (`sort={field}`)**.

---

## 10. CACHE DANH MỤC: IMEMORYCACHE VS REDIS CACHE

### 📌 Mâu thuẫn trong SRS
* **FR-CAT-001 (Trang 23–24):** Dùng `IMemoryCache` (TTL 60 phút).
* **NFR-PERF-003 & NFR-SCALE-001 (Trang 40, 44):** Bắt buộc dùng Redis Distributed Cache (TTL 30 phút), cấm dùng `IMemoryCache`.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-CAT-001 - IMemoryCache):** Nhanh nhưng vi phạm Stateless Backend, không thể scale nhiều container.
* **Phương án B (Theo NFR-SCALE-001 - Redis Cache):** Đồng bộ dữ liệu cho toàn bộ các container backend, đúng chuẩn kiến trúc phân tán.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án B (Redis Cache với TTL = 60 phút)**.

---

## 11. CACHE RECIPE DETAIL: OUTPUT CACHE TTL = 60 PHÚT VS CACHE-ASIDE TTL = 5 PHÚT

### 📌 Mâu thuẫn trong SRS
* **FR-RCP-002 (Trang 28):** Dùng **Output Cache** policy `"RecipeDetail"` với **TTL = 60 phút**, tag `"recipes"`.
* **NFR-PERF-003 (Trang 40):** Quy định: *"Recipe detail: TTL = 5 phút (cache-aside pattern)"*.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-RCP-002 - Output Cache 60 phút):**
  * *Ưu điểm:* Output Cache của .NET 10 lưu cả HTTP Response, trả về cực nhanh mà không cần chạy qua pipeline Minimal API; hỗ trợ Tag-based Eviction (`EvictByTagAsync("recipes")`).
  * *Nhược điểm:* Giữ cache lâu (60 phút), nhưng vì có cơ chế Tag Eviction nên khi recipe cập nhật thì cache bị xóa ngay lập tức.
* **Phương án B (Theo NFR-PERF-003 - Cache-Aside 5 phút):**
  * *Ưu điểm:* Cache ngắn 5 phút giúp dữ liệu tự làm mới thường xuyên.
  * *Nhược điểm:* Mỗi request vẫn phải đi qua pipeline và bốc dữ liệu DTO từ Redis ra serialize lại.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án A (Output Cache với Tag Invalidation)** theo đúng thế mạnh của .NET 10 đã được nêu ở FR-RCP-002.

---

## 12. XỬ LÝ TRÙNG TITLE/SLUG: BÁO LỖI 409 CONFLICT VS TỰ ĐỘNG THÊM SUFFIX SỐ

### 📌 Mâu thuẫn trong SRS
* **FR-RCP-003 dòng A3 (Trang 30):** Ghi: `A3 – Slug đã tồn tại (title trùng): HTTP 409 Conflict`.
* **Mục 7.2 (Trang 54) & Phụ lục B (Trang 68):** Mục 7.2 ghi: *"Title Unique không bắt buộc (có thể trùng title khác nhau slug)"*. Phụ lục B ghi: *"Slug đã tồn tại — tự động thêm suffix (slug-1, slug-2...)"*.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-RCP-003 dòng A3):** Chặn không cho tạo bài viết có title trùng slug cũ, trả về 409 Conflict.
  * *Nhược điểm:* Trải nghiệm người dùng kém (hai tác giả khác nhau cùng viết về "Phở Bò" thì người thứ hai bị chặn không cho đăng).
* **Phương án B (Theo Mục 7.2 & Phụ lục B):** Tự động thêm hậu tố số (`pho-bo-1`, `pho-bo-2`).
  * *Ưu điểm:* Trải nghiệm người dùng tuyệt vời; ai cũng đăng được bài mang tên món ăn mình thích; URL vẫn đảm bảo unique chuẩn SEO.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án B (Tự động thêm suffix số)** để thân thiện với người dùng và nhất quán với Phụ lục B.

---

## 13. RECIPEIMAGE: CỘT OWNED ENTITY VS BẢNG RIÊNG (1:N)

### 📌 Mâu thuẫn trong SRS
* **Bảng 6.4 (Trang 52):** Ghi là `"RecipeImages" (owned — cột trong Recipes)`.
* **Mục 7.5 (Trang 58) & API 8.4 (Trang 64):** Ghi là bảng riêng `RecipeImages` quan hệ 1:N.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo Bảng 6.4):** Sai về mặt kỹ thuật quan hệ $1:N$ trong RDBMS.
* **Phương án B (Theo Mục 7.5 & API 8.4):** Bảng riêng có `Id uuid PK`, `RecipeId FK` quan hệ 1:N.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án B (Bảng riêng 1:N)** (Đính chính lỗi viết nhầm ở Bảng 6.4).

---

## 14. CHUẨN RESPONSE API: ENVELOPE `{ DATA, META }` VS DTO PHẲNG

### 📌 Mâu thuẫn trong SRS
* **Mục 5.2 (Trang 47) & Chương 8 (Trang 61):** Bắt buộc bọc Envelope: `{ "data": {...}, "meta": {...} }`.
* **Chương 3 (Use Cases):** Trả về DTO phẳng trực tiếp hoặc cấu trúc `PagedResult<T>`.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo Mục 5.2):** Bọc 100% response vào `{ data, meta }`.
* **Phương án B (Theo Chương 3):** Trả về DTO trực tiếp cho single resource, `PagedResult` cho danh sách phân trang.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án B (DTO trực tiếp + PagedResult)** để code Minimal APIs gọn gàng và Frontend dễ parse dữ liệu.

---

## 15. KÍCH THƯỚC TRANG MẶC ĐỊNH (DEFAULT PAGESIZE): 12 VS 10

### 📌 Mâu thuẫn trong SRS
* **Chương 3 (FR-RCP-001, FR-SRCH-004, Trang 28, 37):** Mặc định `pageSize = 12`.
* **Chương 8 (API Spec 8.0, 8.2, Trang 61, 62):** Mặc định `pageSize = 10`.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo Chương 3 - PageSize = 12):**
  * *Ưu điểm:* 12 là bội số chung hoàn hảo của 2, 3, 4, 6. Khi hiển thị danh sách dạng lưới (Grid Card) trên web: màn hình Mobile (2 cột), Tablet (3 cột), Desktop (4 cột) đều hiển thị **vừa khít hàng ngang**, không bao giờ bị lẻ thẻ trống ở hàng cuối cùng.
* **Phương án B (Theo Chương 8 - PageSize = 10):**
  * *Nhược điểm:* Hiển thị trên Grid 3 cột hoặc 4 cột sẽ bị lẻ (10 chia 3 dư 1, 10 chia 4 dư 2), tạo khoảng trắng xấu trên UI.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án A (PageSize = 12)** để tối ưu giao diện Frontend.

---

## 16. THUỘC TÍNH USER KHI ĐĂNG KÝ: `{ FULLNAME, USERNAME }` VS `{ DISPLAYNAME }`

### 📌 Mâu thuẫn trong SRS
* **FR-AUTH-001 (Trang 17):** Body: `{ fullName, email, userName, password }`.
* **API Spec 8.1 (Trang 61):** Body: `{ email, password, displayName }`.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-AUTH-001):** Nhận đủ `fullName` (tên hiển thị có dấu) và `userName` (tên định danh không dấu duy nhất). Khớp 100% với migration thực tế trong dự án.
* **Phương án B (Theo API 8.1):** Chỉ nhận `displayName` $\rightarrow$ Thiếu `userName` để làm URL profile tác giả `/authors/{username}`.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án A (Nhận `fullName` và `userName`)**.

---

## 17. RÀNG BUỘC `COOKTIME`: `COOKTIME > 0` VS `COOKTIME >= 0`

### 📌 Mâu thuẫn trong SRS
* **FR-RCP-003 (Trang 29):** Validation bắt buộc `cookTime > 0`.
* **Mô hình Dữ liệu 7.2 (Trang 55):** Ràng buộc SQL: `CookTime integer NOT NULL, CHECK >= 0` (*"0 cho 'No cook' recipes"*).

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo FR-RCP-003):** Bắt buộc `> 0` $\rightarrow$ Chặn các món không cần nấu nhiệt (salad, gỏi cuốn, sinh tố).
* **Phương án B (Theo Mục 7.2):** Cho phép `CookTime >= 0` $\rightarrow$ Đúng thực tế ẩm thực và đúng ràng buộc database.
* **👉 Đề xuất lựa chọn:** **Chọn Phương án B (`CookTime >= 0`)**.

---

## 18. REFRESH TOKEN: 512-BIT QUA BODY VS 128-BIT QUA COOKIE

### 📌 Mâu thuẫn trong SRS
* **FR-AUTH-001 & Mục 5.2 (Trang 18, 47):** Token sinh ngẫu nhiên 512-bit, truyền qua Request Body.
* **NFR-SEC-002 & NFR-SEC-005 (Trang 41):** Token sinh từ 128-bit, truyền qua HTTP-Only Cookie.

### 💡 So sánh 2 phương án từ SRS
* **Phương án A (Theo Mục 5.2):** Truyền qua Request Body $\rightarrow$ Dễ test trên Scalar UI, nhưng có nguy cơ XSS phía client.
* **Phương án B (Theo NFR-SEC-005):** Truyền qua HTTP-Only Cookie $\rightarrow$ Chống tấn công XSS 100%, bảo mật cao nhất.
* **👉 Đề xuất lựa chọn:** Sinh **256-bit hoặc 512-bit**, hash SHA-256 lưu DB; trong môi trường Dev hỗ trợ truyền qua Request Body để dễ kiểm thử, môi trường Prod cấu hình qua HTTP-Only Cookie.

---

## 📊 BẢNG TỔNG HỢP 18 QUYẾT ĐỊNH CHỌN PHƯƠNG ÁN

| STT | Vấn đề mâu thuẫn | Phương án chọn | Căn cứ vững chắc trong SRS |
| :---: | :--- | :--- | :--- |
| **1** | **Xóa Recipe** | **Soft Delete** (`IsDeleted = true`) | NFR-REL-003, Mục 7.1/7.2, Phụ lục A |
| **2** | **Xóa Category** | **Hard Delete** (`Remove`) | FR-CAT-005 (tránh xung đột Unique Constraint) |
| **3** | **Publish Recipe** | **Bắt buộc cả Steps $\ge 1$ và Ingredients $\ge 1$** | Phụ lục A, Phụ lục B (`RECIPE_PUBLISH_INCOMPLETE`), NFR-SEO-001 |
| **4** | **Bước nấu (RecipeStep)** | **Backend tự sinh StepNumber; Title tùy chọn** | Kết hợp tính tự động FR-RCP-010 và schema 7.3 |
| **5** | **HTTP Status Validation** | **HTTP 422 Unprocessable Entity** | Toàn bộ 10 use case tại Chương 3 và chuẩn RFC 7807 |
| **6** | **HTTP Status Concurrency**| **HTTP 409 Conflict** | FR-RCP-004 và chuẩn HTTP RFC 7231 |
| **7** | **Nguyên liệu** | **`Quantity` & `Unit` Nullable** | Mô hình 7.4 (hỗ trợ gia vị nêm nếm "vừa đủ") |
| **8** | **Dinh dưỡng** | **Đầy đủ 6 trường** (thêm Fiber, Sodium) | Mô hình 7.2.1 (hỗ trợ SEO Google Recipe) |
| **9** | **Tham số Sắp xếp** | **`sort={field}`** (tiền tố `"-"` giảm dần) | Toàn bộ Use Cases tại Chương 3 (FR-RCP-001, FR-SRCH-003) |
| **10** | **Cache Danh mục** | **Redis Distributed Cache (TTL = 60 phút)** | NFR-SCALE-001 (nghiêm cấm IMemoryCache) |
| **11** | **Cache Recipe Detail** | **Output Cache TTL = 60 phút kèm Tag Eviction** | FR-RCP-002 (tận dụng công nghệ .NET 10) |
| **12** | **Trùng Title / Slug** | **Tự động thêm suffix số (`-1`, `-2`)** | Mô hình 7.2 và Phụ lục B (`RECIPE_SLUG_EXISTS`) |
| **13** | **RecipeImage** | **Bảng riêng (Quan hệ 1:N)** | Mô hình 7.5 và API 8.4 |
| **14** | **Response Format** | **DTO thuần cho Single, PagedResult cho List** | Toàn bộ DTO tại Chương 3 |
| **15** | **Default PageSize** | **`pageSize = 12`** | FR-RCP-001, FR-SRCH-004 (vừa khít lưới UI 2, 3, 4 cột) |
| **16** | **User Đăng ký** | **Nhận cả `fullName` và `userName`** | FR-AUTH-001 và Migration thực tế của project |
| **17** | **CookTime** | **Cho phép `CookTime >= 0`** | Ràng buộc DB `CHECK >= 0` tại Mục 7.2 |
| **18** | **Refresh Token** | **512-bit / 256-bit hash SHA-256 lưu DB** | FR-AUTH-001 và Mục 7.8 |
