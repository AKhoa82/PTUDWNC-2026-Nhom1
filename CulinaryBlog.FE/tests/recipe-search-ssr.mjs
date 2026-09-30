// Run with Next.js on port 3000 and port 5018 free for this API fixture.
import { createServer } from 'node:http';
import assert from 'node:assert/strict';
const requests = [];
const server = createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  requests.push(url);
  res.setHeader('Content-Type', 'application/json');
  if (url.searchParams.get('q') === 'failure') { res.writeHead(503); res.end('{}'); return; }
  const empty = url.searchParams.get('q') === 'nothing';
  res.end(JSON.stringify({
    items: empty ? [] : [{ id: '1', title: 'Phở bò', description: 'Món ngon', categoryName: 'Món chính', cookingTimeMinutes: 30, relevanceScore: 0.5 }],
    totalCount: empty ? 0 : 6, totalPages: empty ? 0 : 3, page: Number(url.searchParams.get('page')),
    hasNextPage: !empty, hasPreviousPage: !empty, message: empty ? 'Hãy thử từ khóa khác.' : null,
  }));
});
await new Promise((resolve, reject) => { server.once('error', reject); server.listen(5018, resolve); });
const get = async query => (await fetch(`http://127.0.0.1:3000/search${query}`)).text();
try {
  for (const query of ['', '?q=a', '?q=%20%20', '?q=!!!', '?q=pho&page=0', '?q=pho&pageSize=51']) {
    const before = requests.length;
    const html = await get(query);
    assert.equal(requests.length, before, 'Invalid/initial search must not call API');
    if (query) assert(html.includes('role="alert"'));
  }
  const html = await get('?q=pho+bo&page=2&pageSize=2');
  const request = requests.at(-1);
  assert.equal(request.pathname, '/api/v1/recipes/search');
  assert.equal(request.searchParams.get('q'), 'pho bo');
  assert.equal(request.searchParams.get('page'), '2');
  assert(html.includes('Phở bò'));
  const form = html.match(/<form\b[\s\S]*?<\/form>/)?.[0];
  assert(form?.includes('action="/search"'));
  assert(!/name="page"/.test(form), 'New search resets to page 1');
  const links = [...html.matchAll(/href="(\/search\?[^" ]+)"/g)].map(match => new URL(match[1].replaceAll('&amp;', '&'), 'http://localhost'));
  assert(links.some(link => link.searchParams.get('page') === '3'));
  for (const link of links) {
    assert.equal(link.searchParams.get('q'), 'pho bo');
    assert.equal(link.searchParams.get('pageSize'), '2');
  }
  assert((await get('?q=nothing')).includes('Hãy thử từ khóa khác.'));
  assert((await get('?q=failure')).includes('Không thể tìm kiếm lúc này.'));
  console.log('Passed: 9 search SSR scenarios.');
} finally { await new Promise(resolve => server.close(resolve)); }
