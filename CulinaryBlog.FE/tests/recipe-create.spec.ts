import { expect, test } from "@playwright/test";

const apiUrl = process.env.E2E_API_URL ?? "http://127.0.0.1:5018/api";

test("FR-RCP-003: tác giả tạo bản nháp với nguyên liệu và bước nấu", async ({ page, request }) => {
  const email = process.env.E2E_ADMIN_EMAIL;
  const password = process.env.E2E_ADMIN_PASSWORD;
  if (!email || !password) throw new Error("Set E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD before running this test.");

  const login = await request.post(`${apiUrl}/v1/auth/login`, { data: { email, password } });
  expect(login.status()).toBe(200);
  const auth = await login.json() as { accessToken: string; accessTokenExpiresAt: string; fullName: string };
  await page.addInitScript(({ token, expiresAt, name }) => {
    localStorage.setItem("accessToken", token);
    localStorage.setItem("accessTokenExpiresAt", expiresAt);
    localStorage.setItem("userName", name);
    localStorage.setItem("authProvider", "email");
  }, { token: auth.accessToken, expiresAt: auth.accessTokenExpiresAt, name: auth.fullName });

  await page.goto("/dashboard/recipes/new");
  await expect(page.getByRole("heading", { name: "Tạo Công Thức Nấu Ăn Mới" })).toBeVisible();

  await page.locator('input[name="title"]').fill("-----");
  await page.locator('textarea[name="description"]').fill("Món thử nghiệm có mô tả.");
  const category = await page.locator('select[name="categoryId"] option').nth(1).getAttribute("value");
  expect(category).toBeTruthy();
  await page.locator('select[name="categoryId"]').selectOption(category!);
  await page.getByRole("button", { name: "Bước tiếp theo" }).click();
  await expect(page.getByText("Tiêu đề phải chứa ký tự có thể tạo đường dẫn công thức.")).toBeVisible();

  const title = `Đậu hũ E2E ${Date.now()}`;
  await page.locator('input[name="title"]').fill(title);
  await page.locator('textarea[name="description"]').fill("");
  await page.getByRole("button", { name: "Bước tiếp theo" }).click();
  await expect(page.getByText("Nhập mô tả công thức.")).toBeVisible();

  await page.locator('textarea[name="description"]').fill("Món đậu hũ dùng để kiểm tra tạo công thức.");
  await page.getByRole("button", { name: "Bước tiếp theo" }).click();
  await page.getByRole("button", { name: "Bước tiếp theo" }).click();
  await page.getByRole("button", { name: "Thêm nguyên liệu" }).click();
  await page.locator('input[name="ingredients.0.name"]').fill("Đậu hũ");
  await page.getByRole("button", { name: "Bước tiếp theo" }).click();
  await page.getByRole("button", { name: "Thêm bước nấu" }).click();
  await page.locator('textarea[name="steps.0.description"]').fill("Chiên đậu hũ vàng đều.");

  const createResponse = page.waitForResponse(response =>
    response.url().endsWith("/api/v1/recipes") && response.request().method() === "POST");
  await page.getByRole("button", { name: "Lưu bản nháp" }).click();
  const created = await createResponse;
  expect(created.status()).toBe(201);
  const recipe = await created.json() as { slug: string; status: string };
  expect(recipe.status).toBe("Draft");
  expect(recipe.slug).toMatch(/^dau-hu-e2e-\d+$/);
  await expect(page).toHaveURL(/\/dashboard\/recipes$/);

  const detail = await request.get(`${apiUrl}/v1/recipes/${recipe.slug}`, {
    headers: { Authorization: `Bearer ${auth.accessToken}` },
  });
  expect(detail.status()).toBe(200);
  const saved = await detail.json() as { title: string; description: string; ingredients: unknown[]; steps: unknown[] };
  expect(saved.title).toBe(title);
  expect(saved.description).toBe("Món đậu hũ dùng để kiểm tra tạo công thức.");
  expect(saved.ingredients).toHaveLength(1);
  expect(saved.steps).toHaveLength(1);
});

test("FR-RCP-003: API trả lỗi cho dữ liệu không hợp lệ và khách chưa đăng nhập", async ({ request }) => {
  const email = process.env.E2E_ADMIN_EMAIL;
  const password = process.env.E2E_ADMIN_PASSWORD;
  if (!email || !password) throw new Error("Set E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD before running this test.");

  const login = await request.post(`${apiUrl}/v1/auth/login`, { data: { email, password } });
  expect(login.status()).toBe(200);
  const { accessToken } = await login.json() as { accessToken: string };
  const payload = {
    title: "-----",
    description: "Mô tả thử nghiệm",
    categoryId: "11111111-0000-0000-0000-000000000001",
    prepTimeMinutes: 10,
    cookingTimeMinutes: 0,
    servings: 2,
    difficulty: 1,
  };
  const invalidTitle = await request.post(`${apiUrl}/v1/recipes`, {
    headers: { Authorization: `Bearer ${accessToken}` }, data: payload,
  });
  expect(invalidTitle.status()).toBe(422);
  expect((await invalidTitle.json()).errors["Request.Title"]).toBeTruthy();

  const invalidDescription = await request.post(`${apiUrl}/v1/recipes`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: { ...payload, title: "Đậu hũ thử nghiệm", description: "" },
  });
  expect(invalidDescription.status()).toBe(422);
  expect((await invalidDescription.json()).errors["Request.Description"]).toBeTruthy();

  const unauthorized = await request.post(`${apiUrl}/v1/recipes`, {
    data: { ...payload, title: "Đậu hũ thử nghiệm" },
  });
  expect(unauthorized.status()).toBe(401);
});
