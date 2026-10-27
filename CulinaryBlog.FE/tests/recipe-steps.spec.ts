import { expect, test } from "@playwright/test";
import { randomUUID } from "node:crypto";

const apiUrl = process.env.E2E_API_URL ?? "http://127.0.0.1:5018/api";
const categoryId = "11111111-0000-0000-0000-000000000001";

test("FR-RCP-010: CRUD bước nấu, đánh số lại và chỉ cập nhật bước đã đổi", async ({ page, request }) => {
  const suffix = Date.now().toString(36);
  async function register(label: string) {
    const response = await request.post(`${apiUrl}/v1/auth/register`, {
      data: {
        username: `e2e_step_${label}_${suffix}`,
        email: `e2e_step_${label}_${suffix}@example.test`,
        fullName: `Tác giả ${label}`,
        password: "RecipeTest123!", confirmPassword: "RecipeTest123!", termsAccepted: true,
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
      title: `Đậu hũ bước nấu ${suffix}`, description: "Kiểm thử bước nấu.", categoryId,
      prepTimeMinutes: 10, cookingTimeMinutes: 10, servings: 2, difficulty: 1,
      instructions: "Chiên đậu hũ.",
      ingredients: [{ name: "Đậu hũ", quantity: 2, unit: "miếng" }],
      steps: [{ description: "Sơ chế đậu hũ." }, { description: "Chiên đậu hũ." }],
    },
  });
  expect(create.status()).toBe(201);
  const recipe = await create.json() as { id: string; slug: string };
  const root = `${apiUrl}/v1/recipes/${recipe.id}/steps`;
  const initialDetail = await request.get(`${apiUrl}/v1/recipes/${recipe.slug}`, { headers });
  const initial = await initialDetail.json() as { steps: Array<{ id: string; title: string; stepNumber: number }> };
  expect(initial.steps.map(step => step.title)).toEqual(["Bước 1", "Bước 2"]);
  const firstId = initial.steps[0].id;

  expect((await request.post(`${root}/`, { headers: otherHeaders,
    data: { description: "Bày ra đĩa." } })).status()).toBe(403);
  expect((await request.post(`${apiUrl}/v1/recipes/${randomUUID()}/steps/`, { headers,
    data: { description: "Bày ra đĩa." } })).status()).toBe(404);
  expect((await request.post(`${root}/`, { headers,
    data: { description: "" } })).status()).toBe(422);

  const added = await request.post(`${root}/`, { headers,
    data: { description: "Bày ra đĩa.", durationMinutes: 2 } });
  expect(added.status()).toBe(201);
  const third = await added.json() as { id: string; stepNumber: number; title: string };
  expect(third).toMatchObject({ stepNumber: 3, title: "Bước 3" });

  expect((await request.put(`${root}/${third.id}`, { headers: otherHeaders,
    data: { description: "Bày ra đĩa." } })).status()).toBe(403);
  expect((await request.put(`${root}/${randomUUID()}`, { headers,
    data: { description: "Bày ra đĩa." } })).status()).toBe(404);
  expect((await request.put(`${root}/${third.id}`, { headers,
    data: { description: "" } })).status()).toBe(422);

  const updated = await request.put(`${root}/${third.id}`, { headers,
    data: { title: "Hoàn thành", description: "Bày ra đĩa.", durationMinutes: 2 } });
  expect(updated.status()).toBe(200);
  expect((await updated.json() as { title: string }).title).toBe("Hoàn thành");

  expect((await request.delete(`${root}/${firstId}`, { headers: otherHeaders })).status()).toBe(403);
  expect((await request.delete(`${root}/${randomUUID()}`, { headers })).status()).toBe(404);
  expect((await request.delete(`${root}/${firstId}`, { headers })).status()).toBe(204);
  const detail = await request.get(`${apiUrl}/v1/recipes/${recipe.slug}`, { headers });
  const steps = (await detail.json() as { steps: Array<{ id: string; stepNumber: number; title: string }> }).steps;
  expect(steps.map(step => step.stepNumber)).toEqual([1, 2]);
  expect(steps.map(step => step.title)).toEqual(["Bước 1", "Hoàn thành"]);

  await page.addInitScript(({ token, expiresAt, name }) => {
    localStorage.setItem("accessToken", token);
    localStorage.setItem("accessTokenExpiresAt", expiresAt);
    localStorage.setItem("userName", name);
    localStorage.setItem("authProvider", "email");
  }, { token: owner.accessToken, expiresAt: owner.accessTokenExpiresAt, name: owner.fullName });
  const stepWrites: string[] = [];
  page.on("request", sent => {
    if (sent.method() === "PUT" && sent.url().includes(`/recipes/${recipe.id}/steps/`))
      stepWrites.push(sent.url());
  });
  const editUrl = `/dashboard/recipes/${recipe.id}/edit?slug=${encodeURIComponent(recipe.slug)}`;
  await page.goto(editUrl);
  await expect(page.locator('input[name="steps.0.title"]')).toHaveValue("Bước 1");
  await page.getByRole("button", { name: "Lưu thay đổi" }).click();
  await expect(page).toHaveURL(/\/dashboard\/recipes$/);
  expect(stepWrites).toHaveLength(0);

  await page.goto(editUrl);
  await expect(page.locator('textarea[name="steps.0.description"]')).toHaveValue("Chiên đậu hũ.");
  await page.locator('textarea[name="steps.0.description"]').fill("Chiên đậu hũ vàng đều.");
  await page.getByRole("button", { name: "Lưu thay đổi" }).click();
  await expect(page).toHaveURL(/\/dashboard\/recipes$/);
  expect(stepWrites).toHaveLength(1);
  expect(stepWrites[0]).toContain(steps[0].id);

  expect((await request.patch(`${apiUrl}/v1/recipes/${recipe.id}/publish`, { headers })).status()).toBe(200);
  expect((await request.delete(`${root}/${third.id}`, { headers })).status()).toBe(204);
  expect((await request.delete(`${root}/${steps[0].id}`, { headers })).status()).toBe(422);
});
