import { APP_BASE_HREF } from '@angular/common';
import { CommonEngine } from '@angular/ssr';
import express from 'express';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';
import bootstrap from './src/main.server';

// GHSA-x288-3778-4hhx (CWE-918, kritik): @angular/ssr <19.2.21, gelen isteğin ham
// Host başlığını doğrulamadan render URL'sine gömüyor — bir istemci Host başlığını
// değiştirerek SSRF/header injection tetikleyebilir. Angular 19.2.21 bunu framework
// içinde (allowedHosts + cloneRequestAndPatchHeaders) düzeltti; 18'de kalındığı için
// vendor'ın düzeltmesiyle yapısal olarak aynı savunma burada, elle uygulanıyor.
// Üretimde gerçek host adı(ları) SSR_ALLOWED_HOSTS ortam değişkeniyle (virgülle
// ayrılmış) eklenmeli — boşsa sadece kendi dinleme portu kabul edilir.
function buildAllowedHosts(port: string | number): Set<string> {
  const extra = (process.env['SSR_ALLOWED_HOSTS'] ?? '')
    .split(',')
    .map(h => h.trim().toLowerCase())
    .filter(Boolean);

  return new Set([`localhost:${port}`, `127.0.0.1:${port}`, ...extra]);
}

// The Express app is exported so that it can be used by serverless Functions.
export function app(): express.Express {
  const server = express();
  const serverDistFolder = dirname(fileURLToPath(import.meta.url));
  const browserDistFolder = resolve(serverDistFolder, '../browser');
  const indexHtml = join(serverDistFolder, 'index.server.html');
  const port = process.env['PORT'] || 4000;
  const allowedHosts = buildAllowedHosts(port);

  const commonEngine = new CommonEngine();

  server.set('view engine', 'html');
  server.set('views', browserDistFolder);

  // Example Express Rest API endpoints
  // server.get('/api/**', (req, res) => { });
  // Serve static files from /browser
  server.get('**', express.static(browserDistFolder, {
    maxAge: '1y',
    index: 'index.html',
  }));

  // All regular routes use the Angular engine
  server.get('**', (req, res, next) => {
    const { protocol, originalUrl, baseUrl, headers } = req;
    const host = (headers.host ?? '').toLowerCase();

    if (!allowedHosts.has(host)) {
      res.status(400).send('Geçersiz istek.');
      return;
    }

    commonEngine
      .render({
        bootstrap,
        documentFilePath: indexHtml,
        url: `${protocol}://${headers.host}${originalUrl}`,
        publicPath: browserDistFolder,
        providers: [{ provide: APP_BASE_HREF, useValue: baseUrl }],
      })
      .then((html) => res.send(html))
      .catch((err) => next(err));
  });

  return server;
}

function run(): void {
  const port = process.env['PORT'] || 4000;

  // Start up the Node server
  const server = app();
  server.listen(port, () => {
    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

run();
