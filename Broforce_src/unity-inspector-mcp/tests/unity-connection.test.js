import test from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import { once } from 'node:events';
import { UnityConnection } from '../unity-connection.js';

async function fixture(t, handler) {
  const sockets = new Set();
  let connections = 0;
  const server = net.createServer(socket => {
    connections++;
    sockets.add(socket);
    socket.on('close', () => sockets.delete(socket));
    socket.on('error', () => {});
    socket.setEncoding('utf8');
    let buffer = '';
    socket.on('data', chunk => {
      buffer += chunk;
      let end;
      while ((end = buffer.indexOf('\n')) >= 0) {
        const line = buffer.slice(0, end);
        buffer = buffer.slice(end + 1);
        if (!line) continue;
        const request = JSON.parse(line);
        handler(request, socket, (result, success = true) => {
          if (!socket.destroyed) socket.write(JSON.stringify({ id: request.id, success,
            ...(success ? { result } : { error: result }) }) + '\n');
        });
      }
    });
  });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  t.after(async () => {
    for (const socket of sockets) socket.destroy();
    await new Promise(resolve => server.close(resolve));
  });
  const client = new UnityConnection({ host: '127.0.0.1', port: server.address().port });
  t.after(() => client.disconnect());
  return { client, port: server.address().port, connections: () => connections };
}

test('concurrent first requests share one connection and route coalesced out-of-order replies by id', async t => {
  const requests = [];
  const ids = new Set();
  const f = await fixture(t, (request, socket) => {
    ids.add(request.id);
    requests.push(request);
    if (requests.length === 20) socket.write(requests.reverse().map(r =>
      JSON.stringify({ id: r.id, success: true, result: r.params.value }) + '\n').join(''));
  });
  const results = await Promise.all(Array.from({ length: 20 }, (_, value) => f.client.sendCommand('echo', { value })));
  assert.deepEqual(results, Array.from({ length: 20 }, (_, i) => i));
  assert.equal(f.connections(), 1);
  assert.equal(ids.size, 20);
  assert.equal(f.client.pending.size, 0);
});

test('UTF-8 split inside a Chinese character survives socket decoding', async t => {
  const f = await fixture(t, (request, socket) => {
    const bytes = Buffer.from(JSON.stringify({ id: request.id, success: true, result: '中文测试' }) + '\n');
    const cut = bytes.indexOf(Buffer.from('中')) + 1;
    socket.write(bytes.subarray(0, cut));
    setTimeout(() => { if (!socket.destroyed) socket.write(bytes.subarray(cut)); }, 10);
  });
  assert.equal(await f.client.sendCommand('echo'), '中文测试');
});

test('late timed-out reply cannot complete a newer request', async t => {
  const f = await fixture(t, (request, socket, reply) => {
    setTimeout(() => reply(request.params.value), request.params.value === 'old' ? 80 : 130);
  });
  await assert.rejects(f.client.sendCommand('echo', { value: 'old' }, { timeoutMs: 25 }), /timeout.*id=/);
  assert.equal(await f.client.sendCommand('echo', { value: 'new' }), 'new');
  assert.equal(f.client.pending.size, 0);
});

test('disconnect rejects every pending command and releases timers', async t => {
  const f = await fixture(t, () => {});
  await f.client.connect();
  const commands = Array.from({ length: 4 }, () => f.client.sendCommand('echo'));
  await new Promise(resolve => setImmediate(resolve));
  f.client.disconnect();
  const results = await Promise.allSettled(commands);
  assert.ok(results.every(r => r.status === 'rejected'));
  assert.equal(f.client.pending.size, 0);
});

test('runtime configuration switches servers, sends token, and never returns the secret', async t => {
  const a = await fixture(t, (request, socket, reply) => reply('A'));
  const b = await fixture(t, (request, socket, reply) => {
    reply(request.token === 'test-only-token' ? 'B' : 'Authentication failed', request.token === 'test-only-token');
  });
  assert.equal(await a.client.sendCommand('ping'), 'A');
  const failed = await a.client.configure({ port: b.port });
  assert.equal(failed.reachable, false);
  assert.match(failed.error, /Authentication/);
  const status = await a.client.configure({ token: 'test-only-token' });
  assert.equal(status.reachable, true);
  assert.equal(status.authenticationConfigured, true);
  assert.ok(!JSON.stringify(status).includes('test-only-token'));
  assert.equal(await a.client.sendCommand('echo'), 'B');
});

test('invalid configuration is rejected without replacing a working connection', async t => {
  const f = await fixture(t, (request, socket, reply) => reply('ok'));
  await f.client.connect();
  const socket = f.client.socket;
  for (const port of [0, -1, 65536, '9999x', 1.5]) await assert.rejects(f.client.configure({ port }), /Port/);
  await assert.rejects(f.client.configure({ host: '' }), /Host/);
  await assert.rejects(f.client.configure({ token: 'x'.repeat(257) }), /Token/);
  assert.equal(f.client.socket, socket);
  assert.equal(await f.client.sendCommand('ping'), 'ok');
});

test('connection refused reports the target rather than inferring a game crash', async t => {
  const reservation = net.createServer();
  reservation.listen(0, '127.0.0.1');
  await once(reservation, 'listening');
  const port = reservation.address().port;
  await new Promise(resolve => reservation.close(resolve));
  const client = new UnityConnection({ host: '127.0.0.1', port });
  t.after(() => client.disconnect());
  await assert.rejects(client.sendCommand('ping'), new RegExp(`Cannot connect to 127.0.0.1:${port}`));
});

test('invalid JSON fails pending commands and the next request reconnects', async t => {
  let bad = true;
  const f = await fixture(t, (request, socket, reply) => {
    if (bad) { bad = false; socket.write('invalid json\n'); } else reply('recovered');
  });
  await assert.rejects(f.client.sendCommand('ping'), /Invalid JSON/);
  await new Promise(resolve => setTimeout(resolve, 10));
  assert.equal(await f.client.sendCommand('ping'), 'recovered');
});

test('oversized requests are rejected before sending', async t => {
  let received = 0;
  const f = await fixture(t, (request, socket, reply) => { received++; reply('ok'); });
  await assert.rejects(f.client.sendCommand('execute_code', { code: 'x'.repeat(1024 * 1024) }), /1 MiB/);
  assert.equal(await f.client.sendCommand('ping'), 'ok');
  assert.equal(received, 1);
});
