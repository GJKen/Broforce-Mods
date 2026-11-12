#!/usr/bin/env node

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  ListResourcesRequestSchema,
  ReadResourceRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";
import { UnityConnection } from './unity-connection.js';
import os from 'os';
import { exec, execFile, spawn } from 'child_process';
import { promisify } from 'util';
import fs from 'fs/promises';
import path from 'path';
import { fileURLToPath } from 'url';

const execAsync = promisify(exec);
const execFileAsync = promisify(execFile);
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCRIPTS_DIR = path.join(__dirname, 'scripts');
const CSHARP_SCRIPTS_DIR = path.join(SCRIPTS_DIR, 'csharp');
const WINDOWS_GAME_PROCESS_NAMES = ['Broforce_beta.exe', 'Broforce.exe'];
const remoteLaunchHost = process.env.UNITY_INSPECTOR_REMOTE_LAUNCH_HOST?.trim();
const remoteLaunchUser = process.env.UNITY_INSPECTOR_REMOTE_LAUNCH_USER?.trim();
const remoteLaunchPassword = process.env.UNITY_INSPECTOR_REMOTE_LAUNCH_PASSWORD;
const remoteLaunchTask = process.env.UNITY_INSPECTOR_REMOTE_LAUNCH_TASK?.trim();
const remoteProcessScope = process.env.UNITY_INSPECTOR_PROCESS_SCOPE?.trim().toLowerCase() === 'remote';
const localLaunchShortcut = process.env.UNITY_INSPECTOR_LOCAL_LAUNCH_URL?.trim()
  || path.join(os.homedir(), 'Desktop', 'Broforce.url');
const launchGameDescription = isRemoteLaunchConfigured()
  ? 'Launch Broforce on the configured remote Windows machine through schtasks.exe. Never launches a local game process.'
  : 'Launch Broforce locally through the desktop Broforce.url shortcut. Waits for the game to become responsive.';

function isRemoteLaunchConfigured() {
  return Boolean(remoteLaunchHost && remoteLaunchTask);
}

async function triggerRemoteLaunch() {
  const schtasksArgs = ['/Run', '/S', remoteLaunchHost, '/TN', remoteLaunchTask];

  // By default schtasks uses the current Windows credentials. If the caller
  // explicitly supplies remote credentials, pass them to schtasks as well.
  // This keeps the normal LAN setup free of SSH and avoids an interactive
  // password prompt that would block the MCP process.
  if (remoteLaunchUser && remoteLaunchPassword !== undefined) {
    schtasksArgs.push('/U', remoteLaunchUser, '/P', remoteLaunchPassword);
  }

  try {
    await execFileAsync('schtasks.exe', schtasksArgs, { windowsHide: true });
  } catch (error) {
    const detail = error.stderr?.trim() || error.message;
    throw new Error(`远程启动任务失败（${remoteLaunchHost}，${remoteLaunchTask}）：${detail}`);
  }
}

function runIgnoredProcess(command, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      stdio: 'ignore',
      windowsHide: true,
    });
    child.once('error', reject);
    child.once('close', (code, signal) => resolve({ code, signal }));
  });
}

function canManageLocalGameProcess() {
  const host = unityClient.host?.toLowerCase();
  return !remoteProcessScope && (!host || ['localhost', '127.0.0.1', '::1', os.hostname().toLowerCase()].includes(host));
}

async function terminateBroforce() {
  if (process.platform === 'win32') {
    for (const processName of WINDOWS_GAME_PROCESS_NAMES) {
      try {
        await runIgnoredProcess('taskkill', ['/F', '/T', '/IM', processName]);
      } catch {
        // The process may already be stopped.
      }
    }
    return;
  }

  for (const processName of ['Broforce.x86_64', 'Broforce']) {
    try {
      await runIgnoredProcess('pkill', ['-9', '-x', processName]);
    } catch {
      // The process may already be stopped.
    }
  }

  try {
    await runIgnoredProcess('pkill', ['-9', '-f', 'Broforce_beta.exe']);
  } catch {
    // The process may already be stopped.
  }
}

// UMM log file tracking
const configuredLogPath = process.env.UNITY_INSPECTOR_UMM_LOG_PATH?.trim();
const UMM_LOG_PATHS = [
  ...(configuredLogPath ? [configuredLogPath] : []),
  path.join(os.homedir(), '.config/r2modmanPlus-local/Broforce/profiles/Default/UMM/Core/Log.txt'),
  path.join(os.homedir(), 'AppData/Roaming/r2modmanPlus-local/Broforce/profiles/Default/BepInEx/LogOutput.log'),
];
let watchLogOffset = 0;

class UnityInspectorClient extends UnityConnection {
  constructor() {
    const rawPort = process.env.UNITY_INSPECTOR_PORT || '9999';
    const port = /^\d+$/.test(rawPort) ? Number(rawPort) : NaN;
    if (!Number.isInteger(port) || port < 1 || port > 65535) {
      throw new Error('UNITY_INSPECTOR_PORT must be an integer from 1 to 65535');
    }
    super({ host: process.env.UNITY_INSPECTOR_HOST?.trim() || null, port,
      token: process.env.UNITY_INSPECTOR_TOKEN || '' });
    this.platform = null;
    this.gamePath = null;
    this.protonPrefix = null;
  }

  async detectPlatform() {
    if (this.platform) return;

    if (os.release().toLowerCase().includes('microsoft')) {
      this.platform = 'wsl';
      console.error('Platform: WSL');
    } else if (os.platform() === 'linux') {
      this.platform = 'linux';
      // Try to find Proton prefix for path translation
      await this.detectGamePaths();
      console.error(`Platform: Linux (game path: ${this.gamePath || 'not found'})`);
    } else {
      this.platform = 'native';
      console.error('Platform: native Windows/macOS');
    }
  }

  async detectGamePaths() {
    // Common Steam library locations
    const steamPaths = [
      path.join(os.homedir(), '.local/share/Steam'),
      path.join(os.homedir(), '.steam/steam'),
    ];

    for (const steamPath of steamPaths) {
      const gamePath = path.join(steamPath, 'steamapps/common/Broforce');
      try {
        await fs.access(gamePath);
        this.gamePath = gamePath;

        // Check for Proton prefix (appid 274190)
        const prefixPath = path.join(steamPath, 'steamapps/compatdata/274190/pfx');
        try {
          await fs.access(prefixPath);
          this.protonPrefix = prefixPath;
          console.error(`Proton prefix found: ${prefixPath}`);
        } catch {
          console.error('No Proton prefix found - assuming native Linux/Distrobox');
        }
        return;
      } catch {
        continue;
      }
    }
    console.error('Could not auto-detect game path');
  }

  translateScreenshotPath(gameSidePath) {
    if (!gameSidePath) return gameSidePath;

    // If it's already a valid absolute Linux path, no translation needed
    if (gameSidePath.startsWith('/')) {
      return gameSidePath;
    }

    if (this.platform === 'wsl') {
      // WSL: game runs on Windows, path comes as forward-slash Windows path
      // Convert C:/Users/... to /mnt/c/Users/...
      const match = gameSidePath.match(/^([A-Za-z]):\/(.*)/);
      if (match) {
        return `/mnt/${match[1].toLowerCase()}/${match[2]}`;
      }
      return gameSidePath;
    }

    if (this.platform === 'linux' && this.protonPrefix) {
      // Proton: game sees Windows-style paths with drive letters
      const match = gameSidePath.match(/^([A-Za-z]):\/(.*)/);
      if (match) {
        const driveLetter = match[1].toLowerCase();
        if (driveLetter === 'z') {
          // Z: drive maps to real Linux root
          return '/' + match[2];
        }
        return path.join(this.protonPrefix, `dosdevices/${driveLetter}:`, match[2]);
      }
    }

    return gameSidePath;
  }

  async getServerHost() {
    await this.detectPlatform();

    if (this.platform === 'wsl') {
      try {
        const result = await execAsync('ip route show default');
        const host = result.stdout.split(' ')[2];
        console.error(`WSL: using Windows host: ${host}`);
        return host;
      } catch (e) {
        console.error('Failed to get WSL host, using fallback: 172.21.80.1');
        return '172.21.80.1';
      }
    }
    return '127.0.0.1';
  }

  async checkHealth() {
    try {
      const result = await this.sendCommand("ping", {});
      return true;
    } catch (error) {
      console.error("Health check failed:", error.message);
      return false;
    }
  }

}

// Find the UMM log file
async function findLogPath() {
  for (const logPath of UMM_LOG_PATHS) {
    try {
      await fs.access(logPath);
      return logPath;
    } catch {
      continue;
    }
  }
  return null;
}

// C# script library functions
function parseScriptMetadata(content) {
  const metadata = {};
  const lines = content.split('\n');
  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed.startsWith('//')) break;

    const match = trimmed.match(/^\/\/\s*#(\w+)\s+(.*)/);
    if (match) {
      const key = match[1];
      const value = match[2].trim();
      if (key === 'args') {
        if (!metadata.args) metadata.args = [];
        metadata.args.push(value);
      } else {
        metadata[key] = value;
      }
    }
  }
  return metadata;
}

async function listCSharpScripts() {
  try {
    await fs.mkdir(CSHARP_SCRIPTS_DIR, { recursive: true });
    const files = await fs.readdir(CSHARP_SCRIPTS_DIR);
    const scripts = [];

    for (const file of files) {
      if (!file.endsWith('.cs')) continue;

      try {
        const filePath = path.join(CSHARP_SCRIPTS_DIR, file);
        const content = await fs.readFile(filePath, 'utf-8');
        const metadata = parseScriptMetadata(content);

        scripts.push({
          file,
          name: metadata.name || file.replace('.cs', ''),
          description: metadata.description || 'No description',
          tags: metadata.tags || '',
          args: metadata.args || [],
          path: filePath,
        });
      } catch (err) {
        console.error(`Error reading script ${file}: ${err.message}`);
      }
    }

    return {
      count: scripts.length,
      scripts,
      scriptsDirectory: CSHARP_SCRIPTS_DIR,
    };
  } catch (err) {
    return {
      error: err.message,
      count: 0,
      scripts: [],
    };
  }
}

async function buildScriptCatalog() {
  const result = await listCSharpScripts();
  if (result.count === 0) {
    return '# C# Script Library\n\nNo scripts available yet. Create .cs files in scripts/csharp/ to add to the library.';
  }

  let catalog = '# C# Script Library\n\n';
  for (const script of result.scripts) {
    let line = `- ${script.name}: ${script.description}`;
    if (script.tags) {
      line += ` [tags: ${script.tags}]`;
    }
    if (script.args.length > 0) {
      line += ` [args: ${script.args.map(a => a.split(':')[0].trim()).join(', ')}]`;
    }
    catalog += line + '\n';
  }
  return catalog;
}

async function readScriptSource(scriptPath) {
  // If it's an absolute path, read directly
  if (path.isAbsolute(scriptPath)) {
    return {
      source: await fs.readFile(scriptPath, 'utf-8'),
      name: path.basename(scriptPath, '.cs'),
    };
  }

  // Otherwise resolve from library
  let resolved = scriptPath;
  if (!resolved.endsWith('.cs')) {
    resolved += '.cs';
  }
  const filePath = path.join(CSHARP_SCRIPTS_DIR, resolved);
  return {
    source: await fs.readFile(filePath, 'utf-8'),
    name: path.basename(resolved, '.cs'),
  };
}

// Test script functions
async function listTestScripts() {
  try {
    const files = await fs.readdir(SCRIPTS_DIR);
    const scripts = [];

    for (const file of files) {
      if (!file.endsWith('.json')) continue;

      try {
        const filePath = path.join(SCRIPTS_DIR, file);
        const content = await fs.readFile(filePath, 'utf-8');
        const script = JSON.parse(content);

        scripts.push({
          name: file,
          title: script.name || file,
          description: script.description || 'No description',
          steps: script.steps?.length || 0,
        });
      } catch (err) {
        console.error(`Error reading script ${file}: ${err.message}`);
      }
    }

    return {
      count: scripts.length,
      scripts: scripts,
      scriptsDirectory: SCRIPTS_DIR,
    };
  } catch (err) {
    return {
      error: err.message,
      count: 0,
      scripts: [],
    };
  }
}

async function runTestScript(scriptNameOrPath, unityClient) {
  try {
    let scriptPath;

    if (path.isAbsolute(scriptNameOrPath)) {
      scriptPath = scriptNameOrPath;
    } else {
      scriptPath = path.join(SCRIPTS_DIR, scriptNameOrPath);
      if (!scriptPath.endsWith('.json')) {
        scriptPath += '.json';
      }
    }

    console.error(`Loading test script: ${scriptPath}`);
    const content = await fs.readFile(scriptPath, 'utf-8');
    const script = JSON.parse(content);

    if (!script.steps || !Array.isArray(script.steps)) {
      throw new Error('Script must have a "steps" array');
    }

    const results = [];
    const startTime = Date.now();

    console.error(`Executing script: ${script.name || scriptNameOrPath}`);
    console.error(`Steps: ${script.steps.length}`);

    for (let i = 0; i < script.steps.length; i++) {
      const step = script.steps[i];
      const stepStartTime = Date.now();

      try {
        console.error(`Step ${i + 1}/${script.steps.length}: ${step.command}`);

        let stepResult = await unityClient.sendCommand(step.command, step.params || {});
        if (step.command === 'take_screenshot' && stepResult && stepResult.path) {
          stepResult.path = unityClient.translateScreenshotPath(stepResult.path);
        }

        // Check if the result indicates a failure (e.g., execute_code can return success: false)
        const resultFailed = stepResult && typeof stepResult === 'object' && stepResult.success === false;

        const stepDuration = Date.now() - stepStartTime;

        if (resultFailed) {
          // Command executed but reported failure
          results.push({
            step: i + 1,
            command: step.command,
            params: step.params,
            success: false,
            error: stepResult.error || 'Command reported failure',
            result: stepResult,
            duration: stepDuration,
          });

          console.error(`Step ${i + 1} failed: ${stepResult.error || 'Command reported failure'}`);
          break;
        } else {
          // Command succeeded
          results.push({
            step: i + 1,
            command: step.command,
            params: step.params,
            success: true,
            result: stepResult,
            duration: stepDuration,
          });

          if (step.wait) {
            console.error(`Waiting ${step.wait}ms...`);
            await new Promise(resolve => setTimeout(resolve, step.wait));
          }
        }
      } catch (err) {
        const stepDuration = Date.now() - stepStartTime;
        results.push({
          step: i + 1,
          command: step.command,
          params: step.params,
          success: false,
          error: err.message,
          duration: stepDuration,
        });

        console.error(`Step ${i + 1} failed: ${err.message}`);
        break;
      }
    }

    const totalDuration = Date.now() - startTime;
    const successCount = results.filter(r => r.success).length;

    return {
      scriptName: script.name || scriptNameOrPath,
      description: script.description,
      totalSteps: script.steps.length,
      completedSteps: results.length,
      successfulSteps: successCount,
      failedSteps: results.length - successCount,
      totalDuration: totalDuration,
      results: results,
    };
  } catch (err) {
    console.error(`Error running test script: ${err.message}`);
    return {
      error: err.message,
      scriptPath: scriptNameOrPath,
    };
  }
}

const unityClient = new UnityInspectorClient();

const server = new Server(
  {
    name: "unity-inspector-mcp",
    version: "1.0.0",
  },
  {
    capabilities: {
      tools: {},
      resources: {},
    },
  }
);

// Define available tools
server.setRequestHandler(ListToolsRequestSchema, async () => {
  // Build dynamic script catalog for execute_script description
  let scriptCatalog = "";
  try {
    const scripts = await listCSharpScripts();
    if (scripts.count > 0) {
      scriptCatalog = "\n\nAvailable library scripts:\n" + scripts.scripts.map(s => {
        let line = `- ${s.name}: ${s.description}`;
        if (s.args.length > 0) {
          line += ` [args: ${s.args.map(a => a.split(':')[0].trim()).join(', ')}]`;
        }
        return line;
      }).join("\n");
    }
  } catch {}

  return {
    tools: [
      {
        name: "configure_connection",
        description: "Change the Unity Inspector target IP/hostname, TCP port or access token at runtime, then test connectivity. Settings last until this MCP process restarts; use UNITY_INSPECTOR_HOST/PORT/TOKEN for persistent configuration. Does not change the game-side listener.",
        inputSchema: {
          type: "object",
          properties: {
            host: { type: "string", description: "Game machine IP or hostname" },
            port: { type: "integer", minimum: 1, maximum: 65535 },
            token: { type: "string", maxLength: 256, description: "Access token configured in the game; empty string clears it. Never returned in results." },
            connectTimeoutMs: { type: "integer", minimum: 100, maximum: 30000 },
            testConnection: { type: "boolean", default: true }
          },
          additionalProperties: false
        }
      },
      {
        name: "connection_status",
        description: "Show the current Unity Inspector target, TCP connection state and whether a token is configured. Does not reveal the token or connect to the game.",
        inputSchema: { type: "object", properties: {} }
      },
      {
        name: "ping",
        description: "Test connection to Unity Inspector",
        inputSchema: {
          type: "object",
          properties: {},
        },
      },
      {
        name: "list_gameobjects",
        description: "List all GameObjects in the scene",
        inputSchema: {
          type: "object",
          properties: {
            includeInactive: {
              type: "boolean",
              description: "Include inactive GameObjects",
              default: true,
            },
            maxResults: {
              type: "number",
              description: "Maximum number of GameObjects to return",
              default: 100,
            },
          },
        },
      },
      {
        name: "inspect_gameobject",
        description: "Inspect a specific GameObject by path",
        inputSchema: {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Path to the GameObject (e.g., '/Player')",
            },
            detailed: {
              type: "boolean",
              description: "Include full component details (default: false for lightweight response)",
              default: false,
            },
          },
          required: ["path"],
        },
      },
      {
        name: "inspect_component",
        description: "Inspect a specific component on a GameObject",
        inputSchema: {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Path to the GameObject (e.g., '/Player')",
            },
            componentType: {
              type: "string",
              description: "Component type name to inspect",
            },
          },
          required: ["path", "componentType"],
        },
      },
      {
        name: "query_gameobjects",
        description: "Search for GameObjects by name or component",
        inputSchema: {
          type: "object",
          properties: {
            namePattern: {
              type: "string",
              description: "Name pattern to search for",
            },
            componentType: {
              type: "string",
              description: "Component type to filter by",
            },
            includeInactive: {
              type: "boolean",
              description: "Include inactive GameObjects",
              default: true,
            },
            maxResults: {
              type: "number",
              description: "Maximum number of results",
              default: 100,
            },
          },
        },
      },
      {
        name: "inspect_player",
        description: "Get detailed information about the player(s)",
        inputSchema: {
          type: "object",
          properties: {},
        },
      },
      {
        name: "list_enemies",
        description: "List all enemies in the scene",
        inputSchema: {
          type: "object",
          properties: {},
        },
      },
      {
        name: "take_screenshot",
        description: "Take a screenshot of the game. Returns the path to the saved screenshot.",
        inputSchema: {
          type: "object",
          properties: {},
        },
      },
      {
        name: "execute_code",
        description: "Execute C# expressions in the Unity context. Uses Mono.CSharp.Evaluator - expects expressions, not statements",
        inputSchema: {
          type: "object",
          properties: {
            code: {
              type: "string",
              description: "C# expression to evaluate. Cannot use 'return' statements",
            },
          },
          required: ["code"],
        },
      },
      {
        name: "modify_component",
        description: "Modify properties of a component on a GameObject",
        inputSchema: {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Path to the GameObject",
            },
            component: {
              type: "string",
              description: "Component type name",
            },
            properties: {
              type: "object",
              description: "Properties to modify",
            },
          },
          required: ["path", "component", "properties"],
        },
      },
      {
        name: "teleport_player",
        description: "Teleport a player to a specific position",
        inputSchema: {
          type: "object",
          properties: {
            x: {
              type: "number",
              description: "X coordinate",
            },
            y: {
              type: "number",
              description: "Y coordinate",
            },
            z: {
              type: "number",
              description: "Z coordinate (optional)",
            },
            playerNum: {
              type: "number",
              description: "Player index 0-3, or -1 for all active players (default: 0)",
            },
          },
          required: ["x", "y"],
        },
      },
      {
        name: "set_player_health",
        description: "Set a player's health",
        inputSchema: {
          type: "object",
          properties: {
            health: {
              type: "number",
              description: "Health value",
            },
            playerNum: {
              type: "number",
              description: "Player index 0-3, or -1 for all active players (default: 0)",
            },
          },
          required: ["health"],
        },
      },
      {
        name: "spawn_entity",
        description: "Spawn an entity at a specific position (limited implementation)",
        inputSchema: {
          type: "object",
          properties: {
            type: {
              type: "string",
              description: "Entity type to spawn",
            },
            x: {
              type: "number",
              description: "X coordinate",
            },
            y: {
              type: "number",
              description: "Y coordinate",
            },
          },
          required: ["type", "x", "y"],
        },
      },
      {
        name: "set_game_speed",
        description: "Set the game speed (time scale)",
        inputSchema: {
          type: "object",
          properties: {
            speed: {
              type: "number",
              description: "Speed multiplier (1.0 = normal, 0.5 = half speed, 2.0 = double speed)",
            },
          },
          required: ["speed"],
        },
      },
      {
        name: "list_campaigns",
        description: "List all available campaigns with their indices",
        inputSchema: {
          type: "object",
          properties: {},
        },
      },
      {
        name: "go_to_level",
        description: "Go directly to a specific campaign level",
        inputSchema: {
          type: "object",
          properties: {
            campaignIndex: {
              type: "number",
              description: "Campaign index (0-27)",
            },
            levelIndex: {
              type: "number",
              description: "Level index within the campaign (0-based)",
            },
          },
          required: ["campaignIndex", "levelIndex"],
        },
      },
      {
        name: "swap_bro",
        description:
          "Swap the current player's bro mid-level. Kills the current bro and spawns the new one at the same position. Integrates with Swap Bros Mod when available, falls back to direct game manipulation.",
        inputSchema: {
          type: "object",
          properties: {
            broName: {
              type: "string",
              description:
                "Bro name (e.g., 'Rambro', 'Brommando', 'B. A. Broracus'). Use list_bros to see available names.",
            },
            playerNum: {
              type: "number",
              description: "Player number 0-3 (default: 0)",
            },
          },
          required: ["broName"],
        },
      },
      {
        name: "set_bro",
        description:
          "Set which bro the player will spawn as next (on death/respawn/next level). Does NOT change the current bro mid-level.",
        inputSchema: {
          type: "object",
          properties: {
            broName: {
              type: "string",
              description:
                "Bro name (e.g., 'Rambro', 'Brommando'). Use list_bros to see available names.",
            },
            playerNum: {
              type: "number",
              description: "Player number 0-3 (default: 0)",
            },
          },
          required: ["broName"],
        },
      },
      {
        name: "list_bros",
        description:
          "List all available bros that can be used with swap_bro and set_bro. Reports the selected bro for the given player (currentBro) and for all active players (currentBros array).",
        inputSchema: {
          type: "object",
          properties: {
            playerNum: {
              type: "number",
              description: "Player number 0-3 for the currentBro readout (default: 0). The bro lists and currentBros array are always returned regardless.",
            },
          },
        },
      },
      {
        name: "list_bromaker_bros",
        description:
          "List custom BroMaker characters and whether each is available on every negotiated peer.",
        inputSchema: {
          type: "object",
          properties: {},
        },
      },
      {
        name: "switch_bromaker_bro",
        description:
          "Switch an active player through BroMaker's normal StoredHero.LoadBro GUI path, including its online availability check and networked hero spawn.",
        inputSchema: {
          type: "object",
          properties: {
            broName: {
              type: "string",
              description: "Exact BroMaker character name (use list_bromaker_bros).",
            },
            playerNum: {
              type: "number",
              description: "Active player number 0-3 (default: 0).",
            },
          },
          required: ["broName"],
        },
      },
      {
        name: "restart_level",
        description:
          "Restart the current level from the beginning. Clears checkpoints and trigger state for a full reset.",
        inputSchema: {
          type: "object",
          properties: {},
        },
      },
      {
        name: "simulate_input",
        description: "Simulate keyboard/controller input",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              description: "Input action (up, down, left, right, fire, jump, special, highFive, gesture, sprint, start, escape)",
            },
            duration: {
              type: "number",
              description: "Hold duration in milliseconds (optional)",
            },
            count: {
              type: "number",
              description: "Number of times to press the key (optional, cannot be used with duration)",
            },
            interval: {
              type: "number",
              description: "Milliseconds between presses when count > 1 (default: 200)",
            },
            player: {
              type: "number",
              description: "Player number (0-3, default: 0)",
            },
          },
          required: ["action"],
        },
      },
      {
        name: "list_test_scripts",
        description: "List all available test scripts in the scripts directory",
        inputSchema: {
          type: "object",
          properties: {},
        },
      },
      {
        name: "wait_for_game",
        description: "Wait for the game to start up and the Unity Inspector TCP server to become responsive. Polls the connection until a ping succeeds or timeout is reached.",
        inputSchema: {
          type: "object",
          properties: {
            timeout: {
              type: "number",
              description: "Maximum time to wait in seconds (default: 60)",
            },
            interval: {
              type: "number",
              description: "Seconds between connection attempts (default: 3)",
            },
          },
        },
      },
      {
        name: "game_state",
        description: "Get a high-level summary of the current game state: scene, game mode, level info, player status, and bro type. Single call to orient yourself.",
        inputSchema: {
          type: "object",
          properties: {},
        },
      },
      {
        name: "read_log",
        description: "Read the UMM mod manager log file. Returns the last N lines by default, or filter by pattern.",
        inputSchema: {
          type: "object",
          properties: {
            lines: {
              type: "number",
              description: "Number of lines to return from the end of the log (default: 50)",
            },
            filter: {
              type: "string",
              description: "Only return lines containing this string (case-insensitive)",
            },
          },
        },
      },
      {
        name: "watch_log",
        description: "Return log entries that appeared since the last call to watch_log. First call returns the last 20 lines as a baseline. Use this for iterative debug loops.",
        inputSchema: {
          type: "object",
          properties: {
            reset: {
              type: "boolean",
              description: "Reset the watch position to the end of the log (default: false)",
            },
          },
        },
      },
      {
        name: "run_test_script",
        description: "Execute a test script containing a sequence of commands. Scripts are JSON files with command sequences that can include wait delays.",
        inputSchema: {
          type: "object",
          properties: {
            script: {
              type: "string",
              description: "Script name (from scripts/ directory) or absolute file path",
            },
          },
          required: ["script"],
        },
      },
      {
        name: "execute_script",
        description: "Compile and run a C# script file in the Unity runtime. Supports full class definitions, MonoBehaviours, Harmony patches, and private member access. Scripts can define Main() for execution and Unload() for cleanup. Use ScriptContext for Harmony instances, logging, and arguments." + scriptCatalog,
        inputSchema: {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Script name (from scripts/csharp/) or absolute file path to a .cs file",
            },
            args: {
              type: "object",
              description: "Optional key-value arguments passed to the script via ScriptContext.Args",
            },
          },
          required: ["path"],
        },
      },
      {
        name: "compile_script",
        description: "Compile a C# script without running it. Returns compilation success/failure and any compiler errors. Useful for validating scripts before execution.",
        inputSchema: {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Script name (from scripts/csharp/) or absolute file path to a .cs file",
            },
          },
          required: ["path"],
        },
      },
      {
        name: "list_scripts",
        description: "List available C# scripts in the script library with their metadata (name, description, tags, arguments).",
        inputSchema: {
          type: "object",
          properties: {},
        },
      },
      {
        name: "unload_script",
        description: "Unload an active C# script, cleaning up its Harmony patches, GameObjects, and calling its Unload() method if defined.",
        inputSchema: {
          type: "object",
          properties: {
            name: {
              type: "string",
              description: "Name of the script to unload",
            },
          },
          required: ["name"],
        },
      },
      {
        name: "launch_game",
          description: launchGameDescription + ' Returns a warning if the game is already running unless restart is true.',
        inputSchema: {
          type: "object",
          properties: {
            vanilla: {
              type: "boolean",
              description: "Launch without mods (default: false)",
            },
            restart: {
              type: "boolean",
              description: "If true, stop the running game first then relaunch (default: false)",
            },
          },
        },
      },
      {
        name: "stop_game",
        description: "Stop the running Broforce process.",
        inputSchema: {
          type: "object",
          properties: {},
        },
      },
    ],
  };
});

// Handle tool calls
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args = {} } = request.params;

  try {
    let result;

    switch (name) {
      case "configure_connection":
        result = await unityClient.configure(args);
        break;

      case "connection_status":
        result = unityClient.getConnectionStatus();
        break;

      case "ping":
        result = await unityClient.sendCommand("ping");
        break;

      case "list_gameobjects":
        const listParams = {
          includeInactive: args.includeInactive ?? true,
          maxResults: args.maxResults ?? 100,
        };
        console.error(`list_gameobjects params:`, listParams);
        result = await unityClient.sendCommand("list_gameobjects", listParams);
        break;

      case "inspect_gameobject":
        if (!args.path) {
          throw new Error("GameObject path is required");
        }
        result = await unityClient.sendCommand("inspect_gameobject", {
          path: args.path,
          detailed: args.detailed ?? false,
        });
        break;

      case "inspect_component":
        if (!args.path) {
          throw new Error("GameObject path is required");
        }
        if (!args.componentType) {
          throw new Error("Component type is required");
        }
        result = await unityClient.sendCommand("inspect_component", {
          path: args.path,
          componentType: args.componentType,
        });
        break;

      case "query_gameobjects":
        result = await unityClient.sendCommand("query_gameobjects", {
          namePattern: args.namePattern,
          componentType: args.componentType,
          includeInactive: args.includeInactive ?? true,
          maxResults: args.maxResults ?? 100,
        });
        break;

      case "inspect_player":
        result = await unityClient.sendCommand("inspect_player");
        break;

      case "list_enemies":
        result = await unityClient.sendCommand("list_enemies");
        break;

      case "take_screenshot":
        result = await unityClient.sendCommand("take_screenshot");
        if (result && result.path) {
          result.path = unityClient.translateScreenshotPath(result.path);
        }
        break;

      case "execute_code":
        if (!args.code) {
          throw new Error("Code is required");
        }
        result = await unityClient.sendCommand("execute_code", {
          code: args.code,
        });
        break;

      case "modify_component":
        if (!args.path || !args.component || !args.properties) {
          throw new Error("Path, component, and properties are required");
        }
        result = await unityClient.sendCommand("modify_component", {
          path: args.path,
          component: args.component,
          properties: args.properties,
        });
        break;

      case "teleport_player":
        if (args.x === undefined || args.y === undefined) {
          throw new Error("X and Y coordinates are required");
        }
        result = await unityClient.sendCommand("teleport_player", {
          x: args.x,
          y: args.y,
          z: args.z,
          playerNum: args.playerNum ?? 0,
        });
        break;

      case "set_player_health":
        if (args.health === undefined) {
          throw new Error("Health value is required");
        }
        result = await unityClient.sendCommand("set_player_health", {
          health: args.health,
          playerNum: args.playerNum ?? 0,
        });
        break;

      case "spawn_entity":
        if (!args.type || args.x === undefined || args.y === undefined) {
          throw new Error("Entity type and coordinates are required");
        }
        result = await unityClient.sendCommand("spawn_entity", {
          type: args.type,
          x: args.x,
          y: args.y,
        });
        break;

      case "set_game_speed":
        if (args.speed === undefined) {
          throw new Error("Speed value is required");
        }
        result = await unityClient.sendCommand("set_game_speed", {
          speed: args.speed,
        });
        break;

      case "list_campaigns":
        result = await unityClient.sendCommand("list_campaigns", {});
        break;

      case "go_to_level":
        if (args.campaignIndex === undefined || args.levelIndex === undefined) {
          throw new Error("Campaign index and level index are required");
        }
        result = await unityClient.sendCommand("go_to_level", {
          campaignIndex: args.campaignIndex,
          levelIndex: args.levelIndex,
        });

        // Health check after level loading
        setTimeout(async () => {
          const isHealthy = await unityClient.checkHealth();
          if (!isHealthy) {
            console.error("WARNING: Game may have crashed after go_to_level");
          }
        }, 3000);
        break;

      case "swap_bro":
        if (!args.broName) {
          throw new Error("Bro name is required");
        }
        result = await unityClient.sendCommand("swap_bro", {
          broName: args.broName,
          playerNum: args.playerNum ?? 0,
        });
        break;

      case "set_bro":
        if (!args.broName) {
          throw new Error("Bro name is required");
        }
        result = await unityClient.sendCommand("set_bro", {
          broName: args.broName,
          playerNum: args.playerNum ?? 0,
        });
        break;

      case "list_bros":
        result = await unityClient.sendCommand("list_bros", {
          playerNum: args.playerNum ?? 0,
        });
        break;

      case "list_bromaker_bros":
        result = await unityClient.sendCommand("list_bromaker_bros", {});
        break;

      case "switch_bromaker_bro":
        if (!args.broName) {
          throw new Error("BroMaker character name is required");
        }
        result = await unityClient.sendCommand("switch_bromaker_bro", {
          broName: args.broName,
          playerNum: args.playerNum ?? 0,
        });
        break;

      case "restart_level":
        result = await unityClient.sendCommand("restart_level", {});
        break;

      case "simulate_input":
        if (!args.action) {
          throw new Error("Input action is required");
        }
        result = await unityClient.sendCommand("simulate_input", {
          action: args.action,
          duration: args.duration,
          count: args.count,
          interval: args.interval,
          player: args.player,
        });
        break;

      case "list_test_scripts":
        result = await listTestScripts();
        break;

      case "wait_for_game": {
        const timeout = (args.timeout ?? 60) * 1000;
        const interval = (args.interval ?? 3) * 1000;
        const startTime = Date.now();
        let attempts = 0;
        let lastError = null;

        while (Date.now() - startTime < timeout) {
          attempts++;
          try {
            // Force a fresh connection each attempt
            unityClient.disconnect();
            await unityClient.connect();
            await unityClient.sendCommand("ping", {});
            const elapsed = ((Date.now() - startTime) / 1000).toFixed(1);
            result = {
              success: true,
              message: `Game is ready (${attempts} attempts, ${elapsed}s)`,
            };
            break;
          } catch (err) {
            lastError = err.message;
            console.error(`Attempt ${attempts}: ${err.message}`);
            await new Promise(resolve => setTimeout(resolve, interval));
          }
        }

        if (!result) {
          result = {
            success: false,
            message: `Game not ready after ${timeout / 1000}s (${attempts} attempts)`,
            lastError,
          };
        }
        break;
      }

      case "game_state":
        result = await unityClient.sendCommand("game_state", {});
        break;

      case "read_log": {
        const logPath = await findLogPath();
        if (!logPath) {
          throw new Error("UMM log file not found");
        }
        const logContent = await fs.readFile(logPath, 'utf-8');
        const allLines = logContent.split('\n');
        let lines = allLines;

        if (args.filter) {
          const filterLower = args.filter.toLowerCase();
          lines = lines.filter(l => l.toLowerCase().includes(filterLower));
        }

        const count = args.lines ?? 50;
        lines = lines.slice(-count);

        result = {
          path: logPath,
          totalLines: allLines.length,
          returnedLines: lines.length,
          content: lines.join('\n'),
        };
        break;
      }

      case "watch_log": {
        const logPath = await findLogPath();
        if (!logPath) {
          throw new Error("UMM log file not found");
        }

        const stat = await fs.stat(logPath);
        const fileSize = stat.size;

        if (args.reset) {
          watchLogOffset = fileSize;
          result = {
            message: "Watch position reset to end of log",
            fileSize,
          };
          break;
        }

        if (watchLogOffset === 0 || watchLogOffset > fileSize) {
          // First call or log was truncated/rotated — return last 20 lines as baseline
          const logContent = await fs.readFile(logPath, 'utf-8');
          const lines = logContent.split('\n').slice(-20);
          watchLogOffset = fileSize;
          result = {
            isBaseline: true,
            newLines: lines.length,
            content: lines.join('\n'),
          };
          break;
        }

        if (watchLogOffset === fileSize) {
          result = {
            newLines: 0,
            content: "",
            message: "No new log entries",
          };
          break;
        }

        // Read only the new bytes
        const fh = await fs.open(logPath, 'r');
        const bytesToRead = fileSize - watchLogOffset;
        const buffer = Buffer.alloc(bytesToRead);
        await fh.read(buffer, 0, bytesToRead, watchLogOffset);
        await fh.close();
        watchLogOffset = fileSize;

        const newContent = buffer.toString('utf-8');
        const newLines = newContent.split('\n').filter(l => l.length > 0);
        result = {
          newLines: newLines.length,
          content: newContent.trimEnd(),
        };
        break;
      }

      case "run_test_script":
        if (!args.script) {
          throw new Error("Script name or path is required");
        }
        result = await runTestScript(args.script, unityClient);
        break;

      case "execute_script": {
        if (!args.path) {
          throw new Error("Script path is required");
        }
        const scriptData = await readScriptSource(args.path);
        result = await unityClient.sendCommand("execute_script", {
          source: scriptData.source,
          name: scriptData.name,
          args: args.args || {},
        });
        break;
      }

      case "compile_script": {
        if (!args.path) {
          throw new Error("Script path is required");
        }
        const compileData = await readScriptSource(args.path);
        result = await unityClient.sendCommand("compile_script", {
          source: compileData.source,
          name: compileData.name,
        });
        break;
      }

      case "list_scripts":
        result = await listCSharpScripts();
        break;

      case "unload_script":
        if (!args.name) {
          throw new Error("Script name is required");
        }
        result = await unityClient.sendCommand("unload_script", {
          name: args.name,
        });
        break;

      case "launch_game": {
        // Check if game is already running
        let gameRunning = false;
        try {
          unityClient.disconnect();
          await unityClient.connect();
          await unityClient.sendCommand("ping", {});
          gameRunning = true;
        } catch {
          gameRunning = false;
        }

        if (gameRunning && !args.restart) {
          result = {
            success: true,
            alreadyRunning: true,
            message: "Game is already running. Use restart: true to stop and relaunch.",
          };
          break;
        }

        if (!isRemoteLaunchConfigured() && !canManageLocalGameProcess()) {
          throw new Error('The target is remote. Configure a remote launch task or start the game on that machine.');
        }

        if (gameRunning && args.restart) {
          if (isRemoteLaunchConfigured()) {
            throw new Error('Remote restart is not configured; stop the remote game before launching it again.');
          }

          // Stop the game first
          unityClient.disconnect();
          await terminateBroforce();
          // Brief wait for process to fully exit
          await new Promise(resolve => setTimeout(resolve, 1000));
        }

        try {
          if (isRemoteLaunchConfigured()) {
            if (args.vanilla) {
              throw new Error('Remote launch does not support vanilla mode.');
            }

            await triggerRemoteLaunch();
          } else {
            // The local Windows install is launched through the same Steam shortcut
            // the user uses, so Steam/r2modman starts the configured mod profile.
            const localCommand = process.platform === 'win32' ? 'explorer.exe' : 'bf';
            const localArgs = process.platform === 'win32'
              ? [localLaunchShortcut]
              : (args.vanilla ? ["--vanilla"] : []);
            if (process.platform === 'win32' && args.vanilla) {
              throw new Error('Local desktop shortcut does not support vanilla mode.');
            }
            const localProcess = spawn(localCommand, localArgs, {
              detached: true,
              stdio: "ignore",
              windowsHide: true,
            });
            await new Promise((resolve, reject) => {
              localProcess.once('error', reject);
              localProcess.once('spawn', resolve);
            });
            localProcess.unref();
          }

          // Wait for the game to become responsive
          const launchTimeout = 60000;
          const launchInterval = 3000;
          const launchStart = Date.now();
          let launchAttempts = 0;

          while (Date.now() - launchStart < launchTimeout) {
            launchAttempts++;
            await new Promise(resolve => setTimeout(resolve, launchInterval));
            try {
              unityClient.disconnect();
              await unityClient.connect();
              await unityClient.sendCommand("ping", {});
              result = {
                success: true,
                message: `Game ${args.restart ? 'restarted' : 'launched'} and ready (${launchAttempts} attempts, ${((Date.now() - launchStart) / 1000).toFixed(1)}s)`,
              };
              break;
            } catch {
              console.error(`Launch attempt ${launchAttempts}: waiting...`);
            }
          }

          if (!result) {
            result = {
              success: false,
              message: `Game launched but not responsive after ${launchTimeout / 1000}s`,
            };
          }
        } catch (err) {
          throw new Error(`Failed to launch game: ${err.message}`);
        }
        break;
      }

      case "stop_game": {
        if (isRemoteLaunchConfigured() || !canManageLocalGameProcess()) {
          throw new Error('Remote game termination is not configured. Stop Broforce on the target machine.');
        }
        // Disconnect TCP first
        unityClient.disconnect();
        try {
          await terminateBroforce();
          result = { success: true, message: "Game stopped" };
        } catch {
          result = { success: true, message: "Game may already be stopped" };
        }
        break;
      }

      default:
        throw new Error(`Unknown tool: ${name}`);
    }

    return {
      content: [
        {
          type: "text",
          text: JSON.stringify(result, null, 2),
        },
      ],
    };
  } catch (error) {
    console.error(`Tool error: ${error.message}`);
    return {
      content: [
        {
          type: "text",
          text: `Error: ${error.message}`,
        },
      ],
      isError: true,
    };
  }
});

// MCP Resource: C# Script Library Catalog
server.setRequestHandler(ListResourcesRequestSchema, async () => {
  return {
    resources: [
      {
        uri: "scripts://csharp/catalog",
        name: "C# Script Library",
        description: "Available C# scripts for Unity runtime execution",
        mimeType: "text/plain",
      },
    ],
  };
});

server.setRequestHandler(ReadResourceRequestSchema, async (request) => {
  if (request.params.uri === "scripts://csharp/catalog") {
    const catalog = await buildScriptCatalog();
    return {
      contents: [
        {
          uri: "scripts://csharp/catalog",
          text: catalog,
          mimeType: "text/plain",
        },
      ],
    };
  }
  throw new Error(`Unknown resource: ${request.params.uri}`);
});

// Prevent unhandled rejections from crashing the MCP server
// (e.g., TCP socket errors when the game closes)
process.on('unhandledRejection', (reason) => {
  console.error('Unhandled rejection:', reason);
});

// Cleanup on exit
process.on('SIGINT', () => {
  console.error('Shutting down Unity Inspector MCP server...');
  unityClient.disconnect();
  process.exit(0);
});

process.on('SIGTERM', () => {
  unityClient.disconnect();
  process.exit(0);
});

// Start the server
async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("Unity Inspector MCP server running on stdio");
}

main().catch((error) => {
  console.error("Server error:", error);
  process.exit(1);
});
