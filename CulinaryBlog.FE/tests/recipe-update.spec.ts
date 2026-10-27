import { expect, test } from "@playwright/test";
import { randomUUID } from "node:crypto";

const apiUrl = process.env.E2E_API_URL ?? "http://127.0.0.1:5018/api";
const categoryId = "11111111-0000-0000-0000-000000000001";

test("FR-RCP-004: chủ sở hữu cập nhật, người khác bị chặn và RowVersion cũ bị từ chối", async ({ request }) => {
  const suffix = Date.now().toString(36);
  async function register(name: string) {
    const password = "RecipeTest123!";
    const response = await request.post(`${apiUrl}/v1/auth/register`, {
      data: {
        username: `e2e_${name}_${suffix}`,
        email: `e2e_${name}_${suffix}@example.test`,
        fullName: `E2E ${name}`,
        password,
        confirmPassword: password,
        termsAccepted: true,
      },
    });
    expect(response.status()).toBe(201);
    return (await response.json() as { accessToken: string }).accessToken;
  }

  const ownerToken = await register("owner");
  const otherToken = await register("other");
  const initial = {
    title: `Đậu hũ E2E ${suffix}`,
    description: "Mô tả trước khi cập nhật.",
    categoryId,
    prepTimeMinutes: 10,
    cookingTimeMinutes: 0,
    servings: 2,
    difficulty: 1,
    instructions: "Chiên đậu hũ.",
  };
  const create = await request.post(`${apiUrl}/v1/recipes`, {
    headers: { Authorization: `Bearer ${ownerToken}` }, data: initial,
  });
  expect(create.status()).toBe(201);
  const recipe = await create.json() as { id: string; slug: string; rowVersion: string };
  const update = { ...initial, title: `Đậu hũ đã cập nhật ${suffix}`, description: "Mô tả mới.", rowVersion: recipe.rowVersion };
  const url = `${apiUrl}/v1/recipes/${recipe.id}`;

  const forbidden = await request.put(url, {
    headers: { Authorization: `Bearer ${otherToken}` }, data: update,
  });
  expect(forbidden.status()).toBe(403);

  const success = await request.put(url, {
    headers: { Authorization: `Bearer ${ownerToken}` }, data: update,
  });
  expect(success.status()).toBe(200);
  const updated = await success.json() as { title: string; description: string; slug: string; rowVersion: string };
  expect(updated.title).toBe(update.title);
  expect(updated.description).toBe(update.description);
  expect(updated.slug).toBe(recipe.slug);
  expect(updated.rowVersion).not.toBe(recipe.rowVersion);

  const staleNoOp = await request.put(url, {
    headers: { Authorization: `Bearer ${ownerToken}` }, data: update,
  });
  expect(staleNoOp.status()).toBe(409);

  const invalidTitle = await request.put(url, {
    headers: { Authorization: `Bearer ${ownerToken}` },
    data: { ...update, title: "A    ", rowVersion: updated.rowVersion },
  });
  expect(invalidTitle.status()).toBe(422);

  const adminEmail = process.env.E2E_ADMIN_EMAIL;
  const adminPassword = process.env.E2E_ADMIN_PASSWORD;
  if (!adminEmail || !adminPassword) throw new Error("Set E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD before running this test.");
  const adminLogin = await request.post(`${apiUrl}/v1/auth/login`, {
    data: { email: adminEmail, password: adminPassword },
  });
  expect(adminLogin.status()).toBe(200);
  const adminToken = (await adminLogin.json() as { accessToken: string }).accessToken;
  const adminUpdate = await request.put(url, {
    headers: { Authorization: `Bearer ${adminToken}` },
    data: { ...update, title: `Admin cập nhật ${suffix}`, rowVersion: updated.rowVersion },
  });
  expect(adminUpdate.status()).toBe(200);

  const missing = await request.put(`${apiUrl}/v1/recipes/${randomUUID()}`, {
    headers: { Authorization: `Bearer ${ownerToken}` },
    data: { ...update, rowVersion: updated.rowVersion },
  });
  expect(missing.status()).toBe(404);
});
