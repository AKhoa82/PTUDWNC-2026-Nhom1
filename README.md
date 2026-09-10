# Culinary Blog - Ứng dụng Web Blog Ẩm Thực và Nấu Ăn

Nền tảng web hiện đại cho phép người dùng chia sẻ, khám phá và quản lý các công thức nấu ăn từ nhiều nền ẩm thực khác nhau. Dự án thực hành xuyên suốt học phần **Phát triển Ứng dụng Web Nâng cao**.

---

## 👥 Danh sách Thành viên Nhóm

| MSSV | Họ và Tên | Công việc phụ trách |
| :--- | :--- | :--- |
| **2312647** | Lê Anh Khoa | **FR-RCP-002:** Xem chi tiết công thức<br>**FR-RCP-003:** Tạo công thức<br>**FR-RCP-004:** Cập nhật công thức<br>**FR-RCP-005:** Đăng / Hủy đăng công thức<br>**FR-RCP-006:** Lưu trữ công thức<br>**FR-RCP-007:** Xóa công thức<br>**FR-RCP-009:** Quản lý nguyên liệu của công thức<br>**FR-RCP-010:** Quản lý các bước thực hiện công thức<br>**FR-JOB-002:** Xử lý ảnh và tạo ảnh thumbnail |
| **2212426** | Nguyễn Hoàng Hiếu Nghĩa | **FR-AUTH-001:** Đăng ký tài khoản<br>**FR-AUTH-002:** Đăng nhập bằng Email/Mật khẩu<br>**FR-AUTH-003:** Đăng nhập bằng Google OAuth 2.0<br>**FR-AUTH-004:** Làm mới Access Token<br>**FR-AUTH-005:** Đăng xuất / Thu hồi Token<br>**FR-AUTH-006:** Xem thông tin cá nhân<br>**FR-AUTH-007:** Cập nhật thông tin cá nhân<br>**FR-JOB-001:** Gửi Email chào mừng |
| **2312639** | Vũ Thế Huỳnh | **FR-RCP-001:** Danh sách công thức (phân trang, lọc, sắp xếp)<br>**FR-RCP-008:** Quản lý hình ảnh công thức (Upload / Đặt ảnh chính / Xóa)<br>**FR-SRCH-001:** Tìm kiếm toàn văn<br>**FR-SRCH-002:** Lọc công thức<br>**FR-SRCH-003:** Sắp xếp công thức<br>**FR-SRCH-004:** Phân trang kết quả<br>**FR-FILE-001:** Upload file lên MinIO<br>**FR-FILE-002:** Xóa file khỏi MinIO |
| **2312582** | Trương Bảo Bảo | **FR-CAT-001:** Xem danh sách danh mục<br>**FR-CAT-002:** Xem chi tiết danh mục và các công thức<br>**FR-CAT-003:** Tạo danh mục<br>**FR-CAT-004:** Cập nhật danh mục<br>**FR-CAT-005:** Xóa danh mục<br>**FR-JOB-003:** Tạo Sitemap<br>**FR-OBS-001:** Health Check Endpoints<br>**FR-OBS-002:** Structured Logging<br>**FR-OBS-003:** Distributed Tracing & Metrics |

---

## 🛠 Công nghệ & Kỹ thuật Sử dụng

- **Backend:** .NET 10 Minimal APIs, C#, Entity Framework Core 10
- **Frontend:** Next.js App Router, TypeScript, Tailwind CSS
- **Cơ sở dữ liệu & Cache:** PostgreSQL 16 (Full-Text Search với unaccent), Redis 7
- **Object Storage:** MinIO (S3-compatible) cho quản lý file ảnh công thức
- **Kiến trúc & Design Patterns:**
  - **Clean Architecture** gồm 4 tầng phân tách rõ ràng: Domain, Application, Infrastructure, Presentation
  - **CQRS Pattern** (Command Query Responsibility Segregation) kết hợp với thư viện **MediatR**
  - **Mapster** phục vụ ánh xạ DTO hiệu năng cao tại tầng Application
- **Tài liệu API:** OpenAPI 3.x tích hợp giao diện tương tác **Scalar**

---

## 📁 Cấu trúc Thư mục Chi tiết (Clean Architecture)

```text
CulinaryBlog.sln
│
├── .gitignore
├── README.md
│
├── src/
│   ├── CulinaryBlog.Domain/                 # [Tầng trong cùng] Domain Layer - Business rules cốt lõi
│   │   ├── Entities/                        # Các thực thể: Category.cs, Recipe.cs, RecipeStep.cs, v.v.
│   │   ├── ValueObjects/                    # Các đối tượng giá trị (Slug, EmailAddress, ...)
│   │   ├── Enums/                           # Các kiểu dữ liệu liệt kê (RecipeDifficulty, RecipeStatus)
│   │   ├── Interfaces/                      # Khai báo Abstraction Repository chung (IRepository.cs)
│   │   └── Exceptions/                      # Ngoại lệ đặc thù của Domain (DomainException.cs)
│   │
│   ├── CulinaryBlog.Application/            # [Tầng nghiệp vụ] Application Layer - Use Cases & CQRS
│   │   ├── Common/                          # Các thành phần dùng chung (Models, Mappings, Behaviors)
│   │   │   ├── Models/                      # PaginatedResult.cs (Mô hình phân trang chuẩn)
│   │   │   ├── Mappings/                    # Cấu hình ánh xạ Mapster (MappingConfig.cs)
│   │   │   └── Behaviors/                   # MediatR Pipeline Behaviors (ValidationBehavior, LoggingBehavior)
│   │   ├── Contracts/                       # Giao ước giao tiếp với tầng hạ tầng
│   │   │   └── Persistence/                 # IApplicationDbContext.cs
│   │   ├── DTOs/                            # Data Transfer Objects (CategoryDto.cs, RecipeDto.cs)
│   │   ├── Features/                        # Tổ chức theo Vertical Slices (chia theo tính năng)
│   │   │   ├── Categories/                  # Quản lý danh mục (Commands / Queries)
│   │   │   └── Recipes/                     # Quản lý công thức (Commands / Queries)
│   │   └── DependencyInjection.cs           # Đăng ký DI cho tầng Application
│   │
│   ├── CulinaryBlog.Infrastructure/         # [Tầng hạ tầng] Infrastructure Layer - EF Core, Database, Services
│   │   ├── Configurations/                  # Cấu hình EF Core Fluent API cho từng Entity
│   │   ├── Persistence/                     # Triển khai ApplicationDbContext và Database Migrations
│   │   ├── Repositories/                    # Cài đặt thực tế các Repository Pattern
│   │   ├── Services/                        # Dịch vụ ngoài (MinioFileStorageService, MailKitEmailService)
│   │   └── DependencyInjection.cs           # Đăng ký DI cho tầng Infrastructure
│   │
│   └── CulinaryBlog.API/                    # [Tầng giao diện] Presentation Layer - .NET 10 Minimal APIs
│   │   ├── Endpoints/                       # Nhóm các API Endpoints (CategoryEndpoints.cs, RecipeEndpoints.cs)
│   │   ├── Middlewares/                     # GlobalExceptionMiddleware.cs, CorrelationIdMiddleware.cs
│   │   ├── Properties/                      # Cấu hình launchsettings.json
│   │   ├── appsettings.json                 # Cấu hình hệ thống chung (Connection strings, MinIO, v.v.)
│   │   ├── appsettings.Development.json     # Cấu hình môi trường phát triển cục bộ
│   │   └── Program.cs                       # Entry point, cấu hình OpenAPI, Scalar UI và Pipeline
│
└── tests/
    ├── CulinaryBlog.Application.Tests/      # Unit Tests cho các MediatR Handlers và Validators
    └── CulinaryBlog.Integration.Tests/      # Integration Tests kiểm thử tích hợp API và Database
```

## 🌿 Quy tắc Quản lý Mã nguồn (Git Branching Strategy)

Để đảm bảo tiến độ làm việc nhóm và giữ ổn định cho mã nguồn chung, mọi thành viên phải tuân thủ nghiêm ngặt quy tắc quản lý nhánh sau đây:

### 1. Nguyên tắc cơ bản

Không được phép push trực tiếp code lên nhánh chính (main hoặc master).

Mỗi khi bắt đầu thực hiện một chức năng mới hoặc sửa một lỗi (bug), bắt buộc phải tạo nhánh riêng biệt xuất phát từ nhánh phát triển mới nhất.

### 2. Cú pháp đặt tên nhánh (Branch Naming Convention)

**Đối với tính năng mới:**

```text
/mssv/họ-tên/tên-chức-năng
```


### 3. Quy trình làm việc nhóm (Workflow)

**Lấy code mới nhất từ nhánh chính:**

```bash
git checkout main
git pull origin main
```

**Tạo và chuyển sang nhánh làm việc riêng:**

```bash
git checkout -b feature/khoa/quan-ly-danh-muc
```

**Tiến hành lập trình, commit và đẩy nhánh lên Remote Repository:**

```bash
git add .
git commit -m "feat: implement category management feature"
git push origin feature/khoa/quan-ly-danh-muc
```

**Tạo Pull Request (PR):**

- Truy cập giao diện quản lý Git (GitHub/GitLab), tạo Pull Request từ nhánh cá nhân vào nhánh main.
- Thêm ít nhất 1 thành viên khác trong nhóm làm Reviewer để kiểm tra mã nguồn trước khi tiến hành Merge.

## 🚀 Hướng dẫn Thiết lập Môi trường Cục bộ (Local Setup)

**Clone dự án về máy:**

```bash
git clone <URL-Repository-Cua-Ban>
cd CulinaryBlog
```

**Cấu hình chuỗi kết nối cơ sở dữ liệu:**

- Mở file appsettings.Development.json trong project CulinaryBlog.API
- Cập nhật thông số kết nối PostgreSQL (Host, Port, Database, Username, Password) cho phù hợp với máy cá nhân

**Chạy ứng dụng:**

- Mở file solution CulinaryBlog.sln bằng Visual Studio 2022 (đã cập nhật .NET 10 SDK)
- Đặt project CulinaryBlog.API làm Startup Project và nhấn F5 hoặc Ctrl + F5

**Trải nghiệm tài liệu API trực quan:**

- Truy cập đường dẫn: https://localhost:5001/scalar/v1 trên trình duyệt để khám phá giao diện Scalar UI và kiểm thử API

© 2026 - Culinary Blog Team. All rights reserved.
