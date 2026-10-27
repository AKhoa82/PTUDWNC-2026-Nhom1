import { expect, test } from "@playwright/test";

const apiUrl = process.env.E2E_API_URL ?? "http://127.0.0.1:5018/api";
const categoryId = "11111111-0000-0000-0000-000000000001";

test("FR-RCP-006: lưu trữ ẩn công thức công khai và khôi phục về bản nháp", async ({ page, request }) => {
  const suffix = Date.now().toString(36);
  const password = "RecipeTest123!";
  const register = await request.post(`${apiUrl}/v1/auth/register`, {
    data: {
      username: `e2e_archive_${suffix}`,
      email: `e2e_archive_${suffix}@example.test`,
      fullName: "Tác giả E2E",
      password,
      confirmPassword: password,
      termsAccepted: true,
    },
  });
  expect(register.status()).toBe(201);
  const auth = await register.json() as { accessToken: string; accessTokenExpiresAt: string; fullName: string };
  const headers = { Authorization: `Bearer ${auth.accessToken}` };
  const title = `Đậu hũ lưu trữ E2E ${suffix}`;
  const create = await request.post(`${apiUrl}/v1/recipes`, {
    headers,
    data: {
      title,
      description: "Món thử nghiệm lưu trữ.",
      categoryId,
      prepTimeMinutes: 10,
      cookingTimeMinutes: 10,
      servings: 2,
      difficulty: 1,
      instructions: "Chiên đậu hũ.",
      ingredients: [{ name: "Đậu hũ", quantity: 2, unit: "miếng" }],
      steps: [{ description: "Chiên đậu hũ vàng đều." }],
    },
  });
  expect(create.status()).toBe(201);
  const recipe = await create.json() as { id: string; slug: string };
  const publish = await request.patch(`${apiUrl}/v1/recipes/${recipe.id}/publish`, { headers });
  expect(publish.status()).toBe(200);

  await page.addInitScript(({ token, expiresAt, name }) => {
    localStorage.setItem("accessToken", token);
    localStorage.setItem("accessTokenExpiresAt", expiresAt);
    localStorage.setItem("userName", name);
    localStorage.setItem("authProvider", "email");
  }, { token: auth.accessToken, expiresAt: auth.accessTokenExpiresAt, name: auth.fullName });

  await page.goto("/dashboard/recipes");
  const row = page.getByRole("row").filter({ hasText: title });
  await expect(row).toContainText("Đã xuất bản");
  const archiveResponse = page.waitForResponse(response =>
    response.url().endsWith(`/api/v1/recipes/${recipe.id}/archive`) && response.request().method() === "PATCH");
  await row.getByRole("button", { name: "Lưu trữ" }).click();
  expect((await archiveResponse).status()).toBe(200);
  await expect(row).toContainText("Đã lưu trữ");

  const publicListing = await request.get(`${apiUrl}/v1/recipes?keyword=${encodeURIComponent(title)}`);
  expect(publicListing.status()).toBe(200);
  expect(JSON.stringify(await publicListing.json())).not.toContain(recipe.id);
  const publicDetail = await request.get(`${apiUrl}/v1/recipes/${recipe.slug}`);
  expect(publicDetail.status()).toBe(403);

  const restoreResponse = page.waitForResponse(response =>
    response.url().endsWith(`/api/v1/recipes/${recipe.id}/unarchive`) && response.request().method() === "PATCH");
  await row.getByRole("button", { name: "Khôi phục bản nháp" }).click();
  expect((await restoreResponse).status()).toBe(200);
  await expect(row).toContainText("Bản nháp");
});
