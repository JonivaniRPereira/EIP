import assert from "node:assert/strict";
import { createServer } from "node:http";
import { once } from "node:events";

// Owned loopback source: one HTTP 503, then the restored Host's Demo customers.
export async function createTransientDemoSource(base) {
  assert(/^http:\/\/127\.0\.0\.1:\d+$/.test(base));
  const attempts = [];
  const server = createServer(async (req, res) => {
    const url = new URL(req.url, "http://127.0.0.1");
    if (req.method !== "GET" || url.pathname !== "/customers") {
      res.writeHead(404).end();
      return;
    }
    const attempt = { at: new Date().toISOString(), status: 503 };
    attempts.push(attempt);
    if (attempts.length === 1) {
      res.writeHead(503, { "content-type": "application/json" });
      res.end(
        JSON.stringify({ error: "controlled transient laboratory failure" }),
      );
      return;
    }
    try {
      const response = await fetch(
        `${base}/api/v1/sample/customers${url.search}`,
        {
          signal: AbortSignal.timeout(15000),
          redirect: "error",
        },
      );
      attempt.status = response.status;
      res.writeHead(response.status, { "content-type": "application/json" });
      res.end(await response.text());
    } catch {
      attempt.status = 502;
      res.writeHead(502).end();
    }
  });
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  return {
    endpoint: `http://127.0.0.1:${server.address().port}/customers`,
    attempts,
    close: () =>
      new Promise((resolve, reject) => {
        server.close((error) => (error ? reject(error) : resolve()));
        server.closeAllConnections();
      }),
  };
}
