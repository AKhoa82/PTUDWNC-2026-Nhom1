import { expect, test } from "@playwright/test";

const apiUrl = process.env.E2E_API_URL ?? "http://127.0.0.1:5018/api";
const categoryId = "11111111-0000-0000-0000-000000000001";

test("FR-RCP-005: tác giả xuất bản và hủy xuất bản trên giao diện", async ({ page, request }) => {
  const suffix = Date.now().toString(36);
  const password = "RecipeTest123!";
  const register = await request.post(`${apiUrl}/v1/auth/register`, {
    data: {
      username: `e2e_publish_${suffix}`,
      email: `e2e_publish_${suffix}@example.test`,
      fullName: "Tác giả E2E",
      password,
      confirmPassword: password,
      termsAccepted: true,
    },
  });
  expect(register.status()).toBe(201);
  const auth = await register.json() as { accessToken: string; accessTokenExpiresAt: string; fullName: string };
  const headers = { Authorization: `Bearer ${auth.accessToken}` };
  const baseRecipe = {
    description: "Món thử nghiệm xuất bản.", categoryId,
    prepTimeMinutes: 10, cookingTimeMinutes: 10, servings: 2, difficulty: 1,
    instructions: "Chiên đậu hũ."
  };

  const incomplete = await request.post(`${apiUrl}/v1/recipes`, {
    headers, data: { ...baseRecipe, title: `Bản nháp thiếu bước ${suffix}` },
  });
  expect(incomplete.status()).toBe(201);
  const incompleteId = (await incomplete.json() as { id: string }).id;
  const invalidPublish = await request.patch(`${apiUrl}/v1/recipes/${incompleteId}/publish`, { headers });
  expect(invalidPublish.status()).toBe(422);

  const missingIngredients = await request.post(`${apiUrl}/v1/recipes`, {
    headers,
    data: {
      ...baseRecipe,
      title: `Bản nháp thiếu nguyên liệu ${suffix}`,
      steps: [{ description: "Chiên đậu hũ vàng đều." }],
    },
  });
  expect(missingIngredients.status()).toBe(201);
  const missingIngredientsId = (await missingIngredients.json() as { id: string }).id;
  const invalidIngredientsPublish = await request.patch(
    `${apiUrl}/v1/recipes/${missingIngredientsId}/publish`, { headers });
  expect(invalidIngredientsPublish.status()).toBe(422);

  const title = `Đậu hũ xuất bản E2E ${suffix}`;
  const create = await request.post(`${apiUrl}/v1/recipes`, {
    headers,
    data: {
      ...baseRecipe, title,
      ingredients: [{ name: "Đậu hũ", quantity: 2, unit: "miếng" }],
      steps: [{ description: "Chiên đậu hũ vàng đều." }],
    },
  });
  expect(create.status()).toBe(201);
  const recipe = await create.json() as { id: string };

  await page.addInitScript(({ token, expiresAt, name }) => {
    localStorage.setItem("accessToken", token);
    localStorage.setItem("accessTokenExpiresAt", expiresAt);
    localStorage.setItem("userName", name);
    localStorage.setItem("authProvider", "email");
  }, { token: auth.accessToken, expiresAt: auth.accessTokenExpiresAt, name: auth.fullName });

  await page.goto("/dashboard/recipes");
  const row = page.getByRole("row").filter({ hasText: title });
  await expect(row).toBeVisible();
  const publishResponse = page.waitForResponse(response =>
    response.url().endsWith(`/api/v1/recipes/${recipe.id}/publish`) && response.request().method() === "PATCH");
  await row.getByRole("button", { name: "Xuất bản" }).click();
  expect((await publishResponse).status()).toBe(200);
  await expect(row).toContainText("Đã xuất bản");

  const unpublishResponse = page.waitForResponse(response =>
    response.url().endsWith(`/api/v1/recipes/${recipe.id}/unpublish`) && response.request().method() === "PATCH");
  await row.getByRole("button", { name: "Hủy xuất bản" }).click();
  expect((await unpublishResponse).status()).toBe(200);
  await expect(row).toContainText("Bản nháp");
});
