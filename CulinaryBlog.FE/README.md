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

## Kiểm tra production

```powershell
npm run lint
npm run build
npm run start
```
