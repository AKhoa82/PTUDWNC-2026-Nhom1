import { expect, test } from "@playwright/test";

const apiUrl = process.env.E2E_API_URL ?? "http://127.0.0.1:5018/api";
const categoryId = "11111111-0000-0000-0000-000000000001";

test("FR-RCP-007: tác giả xóa mềm công thức trên giao diện", async ({ page, request }) => {
  const suffix = Date.now().toString(36);
  const password = "RecipeTest123!";
  const register = await request.post(`${apiUrl}/v1/auth/register`, {
    data: {
      username: `e2e_delete_${suffix}`,
      email: `e2e_delete_${suffix}@example.test`,
      fullName: "Tác giả E2E",
      password,
      confirmPassword: password,
      termsAccepted: true,
    },
  });
  expect(register.status()).toBe(201);
  const auth = await register.json() as { accessToken: string; accessTokenExpiresAt: string; fullName: string };
  const headers = { Authorization: `Bearer ${auth.accessToken}` };
  const title = `Đậu hũ xóa mềm E2E ${suffix}`;
  const create = await request.post(`${apiUrl}/v1/recipes`, {
    headers,
    data: {
      title,
      description: "Món thử nghiệm xóa mềm.",
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
  expect((await request.patch(`${apiUrl}/v1/recipes/${recipe.id}/publish`, { headers })).status()).toBe(200);

  await page.addInitScript(({ token, expiresAt, name }) => {
    localStorage.setItem("accessToken", token);
    localStorage.setItem("accessTokenExpiresAt", expiresAt);
    localStorage.setItem("userName", name);
    localStorage.setItem("authProvider", "email");
  }, { token: auth.accessToken, expiresAt: auth.accessTokenExpiresAt, name: auth.fullName });

  await page.goto("/dashboard/recipes");
  const row = page.getByRole("row").filter({ hasText: title });
  await expect(row).toBeVisible();
  await row.getByRole("button", { name: "Xóa", exact: true }).click();
  await expect(row).toContainText("Dữ liệu và ảnh vẫn được lưu để có thể khôi phục.");
  const deleteResponse = page.waitForResponse(response =>
    response.url().endsWith(`/api/v1/recipes/${recipe.id}`) && response.request().method() === "DELETE");
  await row.getByRole("button", { name: "Xác nhận xóa" }).click();
  expect((await deleteResponse).status()).toBe(204);
  await expect(row).toHaveCount(0);

  const publicListing = await request.get(`${apiUrl}/v1/recipes?keyword=${encodeURIComponent(title)}`);
  expect(publicListing.status()).toBe(200);
  expect(JSON.stringify(await publicListing.json())).not.toContain(recipe.id);
  expect((await request.get(`${apiUrl}/v1/recipes/${recipe.slug}`)).status()).toBe(404);
});
