// Run against a local Next.js server configured with the default API URL.
// The fixture owns port 5018 only for this test and never touches a database.
import { createServer } from 'node:http';
import assert from 'node:assert/strict';

const requests = [];
const server = createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  res.setHeader('Content-Type', 'application/json');
  if (url.pathname === '/api/v1/categories') {
    res.end(JSON.stringify([]));
    return;
  }
  requests.push(url.searchParams);
  res.end(JSON.stringify({
    items: [{ id: 'fixture', title: 'Fixture recipe', description: 'SSR test', categoryName: 'Meals', cookingTimeMinutes: 20 }],
    totalCount: 6, page: Number(url.searchParams.get('page')), pageSize: 2,
    totalPages: 3, hasNextPage: true, hasPreviousPage: true,
  }));
});

await new Promise((resolve, reject) => {
  server.once('error', reject);
  server.listen(5018, resolve);
});
try {
  for (const [input, expected] of [
    ['unknown', '-createdAt'], ['', '-createdAt'], ['  title  ', 'title'],
    ...['createdAt', '-createdAt', 'title', '-title', 'cookTime', '-cookTime', 'publishedAt', '-publishedAt'].map(x => [x, x]),
  ]) {
    const query = new URLSearchParams({ sort: input, page: '2', pageSize: '2', keyword: 'rice', maxCookTime: '30' });
    const response = await fetch(`http://127.0.0.1:3000/recipes?${query}`);
    assert.equal(response.status, 200);
    const html = await response.text();
    const selected = html.match(/<option\b[^>]*selected=""[^>]*>/g) ?? [];
    assert(selected.some(tag => tag.includes(`value="${expected}"`)), `selected sort: ${input}`);
    const last = requests.at(-1);
    assert.equal(last.get('sort'), expected);
    assert.equal(last.get('keyword'), 'rice');
    assert.equal(last.get('maxCookTime'), '30');
    const links = [...html.matchAll(/href="(\/recipes\?[^" ]+)"/g)].map(match => new URL(match[1].replaceAll('&amp;', '&'), 'http://localhost'));
    assert(links.some(url => url.searchParams.get('page') === '3'));
    for (const link of links) {
      assert.equal(link.searchParams.get('sort'), expected);
      assert.equal(link.searchParams.get('keyword'), 'rice');
      assert.equal(link.searchParams.get('pageSize'), '2');
    }
    const form = html.match(/<form\b[\s\S]*?<\/form>/)?.[0];
    assert(form?.includes('method="get"'));
    assert(!/name="page"/.test(form), 'Submitting new sorting must reset to page 1');
  }
  console.log('Passed: 11 SSR scenarios (selected sort, API parameters, pagination, form reset).');
} finally {
  await new Promise(resolve => server.close(resolve));
}
