
# CulinaryBlog Frontend

Frontend sử dụng Next.js App Router, TypeScript và Tailwind CSS.

## Chạy local

```powershell
npm install
npm run dev
```

Mở `http://localhost:3000`.

API mặc định dùng `http://localhost:5018/api`. Có thể ghi đè bằng biến môi trường:

```text
NEXT_PUBLIC_API_URL=http://localhost:5018/api
```

Để đăng nhập Google, sao chép `.env.local.example` thành `.env.local`, đặt
`AUTH_SECRET`, `AUTH_GOOGLE_ID` và `AUTH_GOOGLE_SECRET` của ứng dụng OAuth.
Trong Google Cloud Console, đăng ký redirect URI
`http://localhost:3000/api/auth/callback/google`. Cấu hình `Google__ClientId`
của API bằng đúng giá trị `AUTH_GOOGLE_ID` (có thể dùng biến môi trường).

## Kiểm tra production

```powershell
npm run lint
npm run build
npm run start
```

## Kiểm thử E2E tạo công thức

Dùng database thử nghiệm riêng. Chạy API ở `http://localhost:5018` với PostgreSQL,
Redis và tài khoản Admin phát triển (`DevAdmin__Password`); chạy frontend ở
`http://localhost:3000`. Sau đó:

```powershell
$env:E2E_ADMIN_EMAIL = 'admin@culinaryblog.local'
$env:E2E_ADMIN_PASSWORD = '<giá trị DevAdmin__Password>'
npm run test:e2e
```

Có thể đổi địa chỉ bằng `E2E_API_URL` (mặc định
`http://127.0.0.1:5018/api`) và `E2E_WEB_URL` (mặc định
`http://localhost:3000`). Kiểm thử tạo bản nháp thật trong database thử nghiệm.
