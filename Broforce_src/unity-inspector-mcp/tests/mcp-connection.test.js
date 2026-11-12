import test from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import { once } from 'node:events';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';

test('MCP exposes runtime connection tools and applies them through the actual stdio server', async t => {
  const sockets = new Set();
  const game = net.createServer(socket => {
    sockets.add(socket);
    socket.on('close', () => sockets.delete(socket));
    socket.setEncoding('utf8');
    let buffer = '';
    socket.on('data', chunk => {
      buffer += chunk;
      let end;
      while ((end = buffer.indexOf('\n')) >= 0) {
        const request = JSON.parse(buffer.slice(0, end));
        buffer = buffer.slice(end + 1);
        socket.write(JSON.stringify({ id: request.id, success: request.token === 'integration-test-token',
          result: { message: 'pong' }, error: 'Authentication failed' }) + '\n');
      }
    });
  });
  game.listen(0, '127.0.0.1');
  await once(game, 'listening');
  t.after(async () => {
    for (const socket of sockets) socket.destroy();
    await new Promise(resolve => game.close(resolve));
  });
  const client = new Client({ name: 'connection-regression', version: '1.0.0' });
  const transport = new StdioClientTransport({ command: process.execPath,
    args: [fileURLToPath(new URL('../index.js', import.meta.url))], stderr: 'pipe',
    env: { ...process.env, UNITY_INSPECTOR_HOST: '127.0.0.1', UNITY_INSPECTOR_PORT: String(game.address().port), UNITY_INSPECTOR_TOKEN: '' }
  });
  t.after(() => client.close());
  await client.connect(transport);
  const tools = await client.listTools();
  assert.ok(tools.tools.some(tool => tool.name === 'configure_connection'));
  assert.ok(tools.tools.some(tool => tool.name === 'connection_status'));
  const status = JSON.parse((await client.callTool({ name: 'connection_status', arguments: {} })).content[0].text);
  assert.equal(status.connected, false);
  assert.equal(status.port, game.address().port);
  const configured = await client.callTool({ name: 'configure_connection', arguments: { token: 'integration-test-token' } });
  const result = JSON.parse(configured.content[0].text);
  assert.equal(result.reachable, true);
  assert.ok(!JSON.stringify(result).includes('integration-test-token'));
  const ping = await client.callTool({ name: 'ping', arguments: {} });
  assert.equal(JSON.parse(ping.content[0].text).message, 'pong');
  await client.callTool({ name: 'configure_connection', arguments: { host: '192.0.2.1', testConnection: false } });
  const stop = await client.callTool({ name: 'stop_game', arguments: {} });
  assert.equal(stop.isError, true);
  assert.match(stop.content[0].text, /Remote game termination/);
});
