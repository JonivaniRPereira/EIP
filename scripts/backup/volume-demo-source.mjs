import { createServer } from "node:http";
import { once } from "node:events";
import { createHash } from "node:crypto";

// Bounded synthetic fixture served only to the disposable Worker.
export async function createVolumeDemoSource() {
  const sizes = { customers: 200, products: 50, "sales-invoices": 2000 };
  const requests = [];
  const server = createServer((req, res) => {
    const url = new URL(req.url, "http://127.0.0.1");
    const entity = url.pathname.slice(1);
    if (req.method !== "GET" || !Object.hasOwn(sizes, entity)) {
      res.writeHead(404).end();
      return;
    }
    const updatedAt = new Date().toISOString();
    const records = Array.from({ length: sizes[entity] }, (_, i) => {
      if (entity === "customers")
        return {
          code: `L07-C${i}`,
          name: `Cliente sintético ${i}`,
          countryCode: "BR",
          isActive: true,
          updatedAt,
        };
      if (entity === "products")
        return {
          code: `L07-P${i}`,
          name: `Produto sintético ${i}`,
          productType: "Product",
          unitOfMeasure: "UN",
          isActive: true,
          updatedAt,
        };
      return {
        invoiceNumber: `L07-NF${i}`,
        issueDate: "2026-10-19",
        customerCode: `L07-C${i % sizes.customers}`,
        status: "Issued",
        currencyCode: "BRL",
        updatedAt,
        items: [
          {
            lineNumber: 1,
            productCode: `L07-P${i % sizes.products}`,
            quantity: 1,
            unitPrice: 100,
            discountAmount: 0,
          },
        ],
      };
    });
    const body = JSON.stringify(records);
    requests.push({
      entity,
      at: updatedAt,
      records: records.length,
      bytes: Buffer.byteLength(body),
      sha256: createHash("sha256").update(body).digest("hex"),
      updatedSince: url.searchParams.get("updatedSince"),
    });
    res.writeHead(200, { "content-type": "application/json" });
    res.end(body);
  });
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  return {
    sizes,
    requests,
    netRevenue: 200000,
    endpoint: (entity) => `http://127.0.0.1:${server.address().port}/${entity}`,
    close: () =>
      new Promise((resolve, reject) => {
        server.close((error) => (error ? reject(error) : resolve()));
        server.closeAllConnections();
      }),
  };
}
