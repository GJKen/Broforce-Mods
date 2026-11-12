import net from 'node:net';
import { randomUUID } from 'node:crypto';

const MAX_RESPONSE_CHARS = 16 * 1024 * 1024;
const MAX_REQUEST_BYTES = 1024 * 1024;

export function commandTimeout(method, params = {}) {
  if (['execute_code', 'execute_script', 'compile_script'].includes(method)) return 35000;
  if (method === 'unload_script') return 17000;
  if (method === 'take_screenshot') return 10000;
  if (method === 'simulate_input' && params.count > 1) {
    return Math.min(125000, (params.count - 1) * (params.interval ?? 200) + 7000);
  }
  return 7000;
}

// One decoder and one response router per socket. A timed-out response is never
// eligible to complete a different request, even when both arrive in one chunk.
export class UnityConnection {
  constructor({ host = null, port = 9999, token = '', connectTimeoutMs = 5000 } = {}) {
    if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Port must be an integer from 1 to 65535');
    if (typeof token !== 'string' || token.length > 256) throw new Error('Token must contain at most 256 characters');
    this.host = host;
    this.port = port;
    this.token = token;
    this.connectTimeoutMs = connectTimeoutMs;
    this.socket = null;
    this.connected = false;
    this.connectPromise = null;
    this.pending = new Map();
    this.generation = 0;
  }

  async getServerHost() { return '127.0.0.1'; }

  async connect() {
    if (this.connected && this.socket && !this.socket.destroyed) return;
    if (this.connectPromise) return this.connectPromise;
    const attempt = this.openConnection();
    this.connectPromise = attempt;
    try {
      await attempt;
    } finally {
      if (this.connectPromise === attempt) this.connectPromise = null;
    }
  }

  async openConnection() {
    const generation = this.generation;
    const host = this.host || await this.getServerHost();
    if (generation !== this.generation) throw new Error('Connection configuration changed');
    this.host = host;
    const socket = new net.Socket();
    this.socket = socket;
    socket.setEncoding('utf8'); // StringDecoder retains incomplete UTF-8 sequences.
    socket.setNoDelay(true);
    let buffer = '';

    return new Promise((resolve, reject) => {
      let settled = false;
      const finishConnect = (error) => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        if (error) reject(error); else resolve();
      };
      const timer = setTimeout(() => {
        const error = new Error(`Connection timeout at ${host}:${this.port}`);
        finishConnect(error);
        socket.destroy(error);
      }, this.connectTimeoutMs);

      socket.on('connect', () => {
        if (this.socket !== socket || generation !== this.generation) {
          socket.destroy();
          finishConnect(new Error('Connection configuration changed'));
          return;
        }
        this.connected = true;
        finishConnect();
      });
      socket.on('data', (data) => {
        if (this.socket !== socket) return;
        buffer += data;
        let newline;
        while ((newline = buffer.indexOf('\n')) >= 0) {
          const line = buffer.slice(0, newline).trim();
          buffer = buffer.slice(newline + 1);
          if (line.length > MAX_RESPONSE_CHARS) {
            socket.destroy(new Error('Unity Inspector response exceeds size limit'));
            return;
          }
          if (!line) continue;
          let response;
          try { response = JSON.parse(line); } catch {
            socket.destroy(new Error('Invalid JSON response from Unity Inspector'));
            return;
          }
          if (!response || typeof response !== 'object') {
            socket.destroy(new Error('Invalid response from Unity Inspector'));
            return;
          }
          const id = response.id === null || response.id === undefined ? null : String(response.id);
          const pending = this.pending.get(id);
          if (!pending || pending.socket !== socket) continue; // Late or unknown response.
          if (response.success === true) this.finishRequest(id, null, response.result);
          else this.finishRequest(id, new Error(response.error || 'Command failed'));
        }
        if (buffer.length > MAX_RESPONSE_CHARS) {
          socket.destroy(new Error('Unity Inspector response exceeds size limit'));
        }
      });
      socket.on('error', (error) => {
        const detail = error.code === 'ECONNREFUSED'
          ? new Error(`Cannot connect to ${host}:${this.port}. Check the game, enabled mod and listening port.`)
          : error;
        finishConnect(detail);
        if (this.socket === socket) {
          this.connected = false;
          this.rejectPending(detail);
        }
      });
      socket.on('close', () => {
        const error = new Error('Unity Inspector connection closed. Check the game and server settings.');
        finishConnect(error);
        if (this.socket === socket) {
          this.connected = false;
          this.socket = null;
          this.rejectPending(error);
        }
      });
      socket.connect(this.port, host);
    });
  }

  finishRequest(id, error, result) {
    const request = this.pending.get(id);
    if (!request) return;
    this.pending.delete(id);
    clearTimeout(request.timer);
    if (error) request.reject(error); else request.resolve(result);
  }

  rejectPending(error) {
    for (const id of this.pending.keys()) this.finishRequest(id, error);
  }

  async sendCommand(method, params = {}, { timeoutMs = commandTimeout(method, params) } = {}) {
    await this.connect();
    const socket = this.socket;
    if (!this.connected || !socket || socket.destroyed) throw new Error('Unity Inspector is disconnected');
    if (this.pending.size >= 64) throw new Error('Too many pending Unity Inspector requests');
    const id = randomUUID();
    const message = JSON.stringify({ id, method, params, ...(this.token ? { token: this.token } : {}) }) + '\n';
    if (Buffer.byteLength(message, 'utf8') > MAX_REQUEST_BYTES) throw new Error('Request exceeds 1 MiB limit');
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.finishRequest(id, new Error(`Command timeout (${method}, id=${id}). Execution may have started; check game state before retrying.`));
      }, timeoutMs);
      this.pending.set(id, { socket, resolve, reject, timer });
      socket.write(message, (error) => { if (error) this.finishRequest(id, error); });
    });
  }

  getConnectionStatus() {
    return {
      host: this.host || 'auto', port: this.port, connected: this.connected,
      authenticationConfigured: Boolean(this.token), connectTimeoutMs: this.connectTimeoutMs,
      pendingRequests: this.pending.size,
    };
  }

  async configure(options = {}) {
    const host = options.host === undefined ? this.host : options.host;
    const port = options.port === undefined ? this.port : options.port;
    const token = options.token === undefined ? this.token : options.token;
    const timeout = options.connectTimeoutMs ?? this.connectTimeoutMs;
    if (host !== null && (typeof host !== 'string' || !host.trim() || host.length > 253)) {
      throw new Error('Host must be a nonempty IP address or hostname');
    }
    if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Port must be an integer from 1 to 65535');
    if (typeof token !== 'string' || token.length > 256) throw new Error('Token must contain at most 256 characters');
    if (!Number.isInteger(timeout) || timeout < 100 || timeout > 30000) throw new Error('Connect timeout must be 100–30000 ms');
    this.disconnect(new Error('Connection configuration changed'));
    this.host = host === null ? null : host.trim();
    this.port = port;
    this.token = token;
    this.connectTimeoutMs = timeout;
    if (options.testConnection === false) return this.getConnectionStatus();
    try {
      await this.sendCommand('ping');
      return { ...this.getConnectionStatus(), reachable: true };
    } catch (error) {
      return { ...this.getConnectionStatus(), reachable: false, error: error.message };
    }
  }

  disconnect(reason = new Error('Unity Inspector disconnected')) {
    this.generation++;
    this.connected = false;
    this.connectPromise = null;
    const socket = this.socket;
    this.socket = null;
    this.rejectPending(reason);
    if (socket) socket.destroy();
  }
}
