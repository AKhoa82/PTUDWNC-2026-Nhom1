import { expect, test } from "@playwright/test";
import { randomUUID } from "node:crypto";

const apiUrl = process.env.E2E_API_URL ?? "http://127.0.0.1:5018/api";
const categoryId = "11111111-0000-0000-0000-000000000001";

test("FR-RCP-009: CRUD nguyên liệu và chỉ cập nhật mục đã đổi", async ({ page, request }) => {
  const suffix = Date.now().toString(36);
  async function register(label: string) {
    const response = await request.post(`${apiUrl}/v1/auth/register`, {
      data: {
        username: `e2e_ing_${label}_${suffix}`,
        email: `e2e_ing_${label}_${suffix}@example.test`,
        fullName: `Tác giả ${label}`,
        password: "RecipeTest123!",
        confirmPassword: "RecipeTest123!",
        termsAccepted: true,
      },
    });
    expect(response.status()).toBe(201);
    return response.json() as Promise<{ accessToken: string; accessTokenExpiresAt: string; fullName: string }>;
  }
  const owner = await register("owner");
  const other = await register("other");
  const headers = { Authorization: `Bearer ${owner.accessToken}` };
  const otherHeaders = { Authorization: `Bearer ${other.accessToken}` };
  const create = await request.post(`${apiUrl}/v1/recipes`, {
    headers,
    data: {
      title: `Đậu hũ nguyên liệu ${suffix}`,
      description: "Kiểm thử quản lý nguyên liệu.", categoryId,
      prepTimeMinutes: 10, cookingTimeMinutes: 10, servings: 2, difficulty: 1,
      instructions: "Chiên đậu hũ.",
      ingredients: [
        { name: "Đậu hũ", quantity: 2, unit: "miếng", sortOrder: 0 },
        { name: "Dầu ăn", quantity: 1, unit: "muỗng", sortOrder: 1 },
      ],
      steps: [{ description: "Chiên đậu hũ vàng đều." }],
    },
  });
  expect(create.status()).toBe(201);
  const recipe = await create.json() as { id: string; slug: string };
  const root = `${apiUrl}/v1/recipes/${recipe.id}/ingredients`;
  const initialDetail = await request.get(`${apiUrl}/v1/recipes/${recipe.slug}`, { headers });
  expect(initialDetail.status()).toBe(200);
  const initial = await initialDetail.json() as { ingredients: Array<{ id: string; name: string }> };
  const tofuId = initial.ingredients.find(item => item.name === "Đậu hũ")!.id;
  const oilId = initial.ingredients.find(item => item.name === "Dầu ăn")!.id;

  expect((await request.post(root, { headers: otherHeaders, data: { name: "Muối" } })).status()).toBe(403);
  expect((await request.post(`${apiUrl}/v1/recipes/${randomUUID()}/ingredients`,
    { headers, data: { name: "Muối" } })).status()).toBe(404);
  expect((await request.post(root, { headers, data: { name: "", quantity: 0 } })).status()).toBe(422);

  const added = await request.post(root, {
    headers,
    data: { name: "Muối", quantity: null, unit: null, notes: "Vừa đủ", sortOrder: 2 },
  });
  expect(added.status()).toBe(201);
  const salt = await added.json() as { id: string; quantity: number | null; unit: string | null; sortOrder: number };
  expect(salt.quantity).toBeNull();
  expect(salt.unit).toBeNull();
  expect(salt.sortOrder).toBe(2);

  expect((await request.put(`${root}/${salt.id}`, { headers: otherHeaders,
    data: { name: "Muối", quantity: null, unit: null, sortOrder: 2 } })).status()).toBe(403);
  expect((await request.put(`${root}/${randomUUID()}`, { headers,
    data: { name: "Muối", quantity: null, unit: null, sortOrder: 2 } })).status()).toBe(404);
  expect((await request.put(`${root}/${salt.id}`, { headers,
    data: { name: "", quantity: -1, sortOrder: 2 } })).status()).toBe(422);

  const updated = await request.put(`${root}/${salt.id}`, { headers,
    data: { name: "Muối biển", quantity: null, unit: null, notes: "Nêm vừa đủ", sortOrder: 2 } });
  expect(updated.status()).toBe(200);
  expect((await updated.json() as { name: string }).name).toBe("Muối biển");

  expect((await request.delete(`${root}/${oilId}`, { headers: otherHeaders })).status()).toBe(403);
  expect((await request.delete(`${root}/${randomUUID()}`, { headers })).status()).toBe(404);
  expect((await request.delete(`${root}/${oilId}`, { headers })).status()).toBe(204);
  expect((await request.put(`${root}/${salt.id}`, { headers,
    data: { name: "Muối biển", quantity: null, unit: null, notes: "Nêm vừa đủ", sortOrder: 1 } })).status()).toBe(200);

  const detail = await request.get(`${apiUrl}/v1/recipes/${recipe.slug}`, { headers });
  expect(detail.status()).toBe(200);
  const ingredients = (await detail.json() as {
    ingredients: Array<{ id: string; name: string; quantity: number | null; unit: string | null }>;
  }).ingredients;
  expect(ingredients.map(item => item.name)).toEqual(["Đậu hũ", "Muối biển"]);
  expect(ingredients.find(item => item.id === salt.id)).toMatchObject({ quantity: null, unit: null });

  await page.addInitScript(({ token, expiresAt, name }) => {
    localStorage.setItem("accessToken", token);
    localStorage.setItem("accessTokenExpiresAt", expiresAt);
    localStorage.setItem("userName", name);
    localStorage.setItem("authProvider", "email");
  }, { token: owner.accessToken, expiresAt: owner.accessTokenExpiresAt, name: owner.fullName });
  const ingredientWrites: string[] = [];
  page.on("request", sent => {
    if (sent.method() === "PUT" && sent.url().includes(`/recipes/${recipe.id}/ingredients/`))
      ingredientWrites.push(sent.url());
  });
  await page.goto(`/dashboard/recipes/${recipe.id}/edit?slug=${encodeURIComponent(recipe.slug)}`);
  await expect(page.locator('input[name="ingredients.0.name"]')).toHaveValue("Đậu hũ");
  await page.getByRole("button", { name: "Lưu thay đổi" }).click();
  await expect(page).toHaveURL(/\/dashboard\/recipes$/);
  expect(ingredientWrites).toHaveLength(0);

  await page.goto(`/dashboard/recipes/${recipe.id}/edit?slug=${encodeURIComponent(recipe.slug)}`);
  await expect(page.locator('input[name="ingredients.0.name"]')).toHaveValue("Đậu hũ");
  await page.locator('input[name="ingredients.0.name"]').fill("Đậu hũ non");
  await page.getByRole("button", { name: "Lưu thay đổi" }).click();
  await expect(page).toHaveURL(/\/dashboard\/recipes$/);
  expect(ingredientWrites).toHaveLength(1);
  expect(ingredientWrites[0]).toContain(tofuId);

  expect((await request.patch(`${apiUrl}/v1/recipes/${recipe.id}/publish`, { headers })).status()).toBe(200);
  expect((await request.delete(`${root}/${salt.id}`, { headers })).status()).toBe(204);
  expect((await request.delete(`${root}/${tofuId}`, { headers })).status()).toBe(422);
});
