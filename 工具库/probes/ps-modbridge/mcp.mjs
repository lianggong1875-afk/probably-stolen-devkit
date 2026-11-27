#!/usr/bin/env node
/**
 * ps-modbridge — 面向《Probably Stolen / 深空当铺》ModDevBridgeNext 的极简 MCP 客户端。
 *
 * 为什么要有它：这个 MOD 的 MCP 端点只在游戏运行且 MOD 启用后才存在，
 * 而宿主常驻 MCP 客户端（如 dsh-mcp-client）是长连接，游戏一关就断、
 * 重连预算耗尽后工具消失。用一次性脚本按需连接更符合「游戏开 → 连、游戏关 → 不用管」的节奏。
 *
 * 传输：MCP Streamable HTTP（POST JSON-RPC，Accept: application/json, text/event-stream）
 *
 * 用法：
 *   node mcp.mjs instances                     列出发现的 bridge 实例并验活
 *   node mcp.mjs status                        调 get_game_status
 *   node mcp.mjs env                           调 get_development_environment
 *   node mcp.mjs tools                         Tools/list（含每个工具的入参摘要）
 *   node mcp.mjs tool <name> '<json>'|@args.json   调一个工具
 *   node mcp.mjs resources                     Resources/list
 *   node mcp.mjs read <uri>                     Resources/read
 *   node mcp.mjs raw '<json-rpc-payload>'      发原始 JSON-RPC
 *
 * 通用选项：
 *   --url <mcpUrl>         跳过发现，直连指定端点
 *   --instance <id>        多实例时显式指定
 *   --pretty / --compact   输出格式（默认 pretty）
 *   --brief                裁掉观测结果里的冗长固定字段（interpretation/output/observation）
 *   --summary              只输出 entries 的 name/role/text/uiRef 表格
 *   --find <text>          只输出标签含该文本的节点及其引用（拿 targetRef 的最省事方式）
 *   --save <path>          把完整结果另存为 JSON 文件（便于事后核对）
 *   --full                 输出原始 tools/call 返回（含 content 副本）
 *   --timeout <ms>         单次请求超时（默认 60000）
 *
 * 重要：Windows PowerShell 5.1 向原生程序传参会剥掉双引号，含 JSON 的参数极易被拆坏。
 * 复杂参数一律写成文件再用 "@路径" 传入。
 */

import fs from 'node:fs'
import path from 'node:path'

const PROTOCOL_VERSION = '2025-06-18'

// ---------------------------------------------------------------- 参数解析

const argv = process.argv.slice(2)
const opts = { pretty: true, timeout: 60000 }
const positional = []

for (let i = 0; i < argv.length; i++) {
  const a = argv[i]
  if (a === '--url') opts.url = argv[++i]
  else if (a === '--instance') opts.instance = argv[++i]
  else if (a === '--timeout') opts.timeout = Number(argv[++i])
  else if (a === '--pretty') opts.pretty = true
  else if (a === '--compact') opts.pretty = false
  else if (a === '--full') opts.full = true
  else if (a === '--brief') opts.brief = true
  else if (a === '--summary') opts.summary = true
  else if (a === '--find') opts.find = argv[++i]
  else if (a === '--save') opts.save = argv[++i]
  else if (a === '--index') opts.index = Number(argv[++i])
  else if (a === '--force') opts.force = true
  else if (a === '--help' || a === '-h') opts.help = true
  else positional.push(a)
}

// ---------------------------------------------------------------- 实例发现
//
// 注意：dsh-dpx 之类隔离环境会把 LOCALAPPDATA / HOME 收容到环境根，
// 而 MOD 的实例描述写在真实用户的 %LOCALAPPDATA% 下，所以不能用 os.homedir()。

function instanceDirs() {
  const dirs = []
  const push = (p) => { if (p && !dirs.includes(p)) dirs.push(p) }
  if (process.env.MODDEVBRIDGE_RUNTIME_DIR) push(process.env.MODDEVBRIDGE_RUNTIME_DIR)
  const suffix = path.join('ModDevBridgeNext', '.runtime', 'instances')
  push(path.join(process.env.LOCALAPPDATA ?? '', suffix))
  try {
    for (const user of fs.readdirSync('C:\\Users', { withFileTypes: true })) {
      if (!user.isDirectory()) continue
      push(path.join('C:\\Users', user.name, 'AppData', 'Local', suffix))
    }
  } catch {}
  // 不过滤：候选项即使当前不存在也要报出来，排查时才知道找过哪里
  return dirs
}

function loadInstances() {
  const out = []
  for (const dir of instanceDirs()) {
    let files
    try {
      files = fs.readdirSync(dir)
    } catch {
      continue // 目录不存在或不可读
    }
    for (const f of files) {
      if (!f.endsWith('.json')) continue
      const full = path.join(dir, f)
      try {
        out.push({ ...JSON.parse(fs.readFileSync(full, 'utf8')), __file: full })
      } catch (e) {
        out.push({ __file: full, __parseError: String(e) })
      }
    }
  }
  return out
}

/**
 * 验活。优先 GET /bridge/status（权威：返回 instanceId / releaseId /
 * bridgeStartedAtUtc / assemblyPath / disk），失败再退回描述文件里的 diagnosticUrl。
 * 两者都只读，不改变游戏状态。见 MOD 包 references/CONNECTION_DIAGNOSTICS.md。
 */
async function probeStatus(inst) {
  const out = { alive: false, via: null, status: null, error: null }
  const mcpUrl = inst.mcpUrl ?? (inst.port ? `http://127.0.0.1:${inst.port}/mcp` : null)
  if (mcpUrl) {
    try {
      const u = new URL(mcpUrl)
      const statusUrl = `${u.protocol}//${u.host}/bridge/status`
      const r = await fetch(statusUrl, { signal: AbortSignal.timeout(3000) })
      const body = await r.json()
      out.status = body
      out.via = statusUrl
      out.alive = r.ok
      if (r.ok && inst.instanceId && body.instanceId && body.instanceId !== inst.instanceId) {
        out.error = `instanceId 不匹配：描述文件 ${inst.instanceId} ≠ 服务端 ${body.instanceId}（旧描述，需重新发现）`
        out.alive = false
      }
      if (r.ok && inst.releaseId && body.releaseId && body.releaseId !== inst.releaseId) {
        out.error = `releaseId 不匹配：描述文件 ${inst.releaseId} ≠ 运行中 ${body.releaseId}（磁盘包变过，需重启游戏后重新发现）`
        out.alive = false
      }
      return out
    } catch (e) {
      out.error = `/bridge/status 失败: ${e.message}`
    }
  }
  if (inst.diagnosticUrl) {
    try {
      const r = await fetch(inst.diagnosticUrl, { signal: AbortSignal.timeout(3000) })
      out.status = await r.json().catch(() => null)
      out.via = inst.diagnosticUrl
      out.alive = r.ok
      if (r.ok) out.error = null
      return out
    } catch (e) {
      out.error = `${out.error ?? ''} / diagnosticUrl 失败: ${e.message}`.trim()
    }
  }
  return out
}

// ---------------------------------------------------------------- 传输层

class McpSession {
  constructor(url, timeout) {
    this.url = url
    this.timeout = timeout
    this.sessionId = null
    this.nextId = 1
    this.protocolVersion = null
  }

  async post(payload, { isNotification = false } = {}) {
    const headers = {
      'content-type': 'application/json',
      accept: 'application/json, text/event-stream',
    }
    if (this.sessionId) headers['mcp-session-id'] = this.sessionId
    if (this.protocolVersion) headers['mcp-protocol-version'] = this.protocolVersion

    const ac = new AbortController()
    const timer = setTimeout(() => ac.abort(), this.timeout)
    let res
    try {
      res = await fetch(this.url, {
        method: 'POST',
        headers,
        body: JSON.stringify(payload),
        signal: ac.signal,
      })
    } finally {
      clearTimeout(timer)
    }

    const sid = res.headers.get('mcp-session-id')
    if (sid) this.sessionId = sid

    if (isNotification) return { ok: res.ok, status: res.status }

    const ctype = res.headers.get('content-type') ?? ''
    if (!res.ok) {
      const text = await res.text().catch(() => '')
      throw new Error(`HTTP ${res.status} ${res.statusText}\n${text.slice(0, 2000)}`)
    }

    if (ctype.includes('text/event-stream')) return await this.readSse(res, payload.id)

    const text = await res.text()
    if (!text.trim()) return null
    return JSON.parse(text)
  }

  async readSse(res, expectId) {
    const reader = res.body.getReader()
    const decoder = new TextDecoder()
    let buf = ''
    let found = null
    while (!found) {
      const { value, done } = await reader.read()
      if (done) break
      buf += decoder.decode(value, { stream: true })
      let idx
      while ((idx = buf.indexOf('\n\n')) !== -1) {
        const frame = buf.slice(0, idx)
        buf = buf.slice(idx + 2)
        const dataLines = frame
          .split('\n')
          .filter((l) => l.startsWith('data:'))
          .map((l) => l.slice(5).trim())
        if (!dataLines.length) continue
        let msg
        try { msg = JSON.parse(dataLines.join('\n')) } catch { continue }
        if (msg.id === expectId) { found = msg; break }
      }
    }
    try { await reader.cancel() } catch {}
    if (!found) throw new Error('SSE 流结束但没有收到匹配 id 的响应')
    return found
  }

  async initialize() {
    const id = this.nextId++
    const res = await this.post({
      jsonrpc: '2.0',
      id,
      method: 'initialize',
      params: {
        protocolVersion: PROTOCOL_VERSION,
        capabilities: {},
        clientInfo: { name: 'ps-modbridge-cli', version: '0.2.0' },
      },
    })
    if (res?.error) throw new Error(`initialize 失败: ${JSON.stringify(res.error)}`)
    this.protocolVersion = res?.result?.protocolVersion ?? PROTOCOL_VERSION
    await this.post({ jsonrpc: '2.0', method: 'notifications/initialized' }, { isNotification: true })
    return res?.result
  }

  async request(method, params) {
    const id = this.nextId++
    const payload = { jsonrpc: '2.0', id, method }
    if (params !== undefined) payload.params = params
    const res = await this.post(payload)
    if (res?.error) {
      const err = new Error(`JSON-RPC error ${res.error.code}: ${res.error.message}`)
      err.rpcError = res.error
      throw err
    }
    return res?.result
  }

  async close() {
    if (!this.sessionId) return
    try {
      await fetch(this.url, { method: 'DELETE', headers: { 'mcp-session-id': this.sessionId } })
    } catch {}
  }
}

// ---------------------------------------------------------------- 输出投影

function emit(value) {
  const text = typeof value === 'string' ? value : JSON.stringify(value, null, opts.pretty ? 2 : 0)
  process.stdout.write(text + '\n')
}

/**
 * tools/call 返回 { content:[{type:text,text:"<同样的 JSON>"}], structuredContent:{...}, isError }。
 * structuredContent 已含 ok/result/operationId/sessionId/worldId/executionState/error，
 * 再打一遍 content 只是让输出翻倍。默认只输出 structuredContent。
 */
function emitCall(result) {
  if (opts.full) return emit(result)
  const payload = result?.structuredContent ?? result

  if (opts.save) {
    const p = path.resolve(opts.save)
    fs.mkdirSync(path.dirname(p), { recursive: true })
    fs.writeFileSync(p, JSON.stringify(payload, null, 2), 'utf8')
  }
  if (opts.find) {
    const hits = findMatches(payload, opts.find)
    return emit({ find: opts.find, count: hits.length, hits })
  }
  if (opts.summary) return emit(summarizeEntries(payload))
  if (opts.brief) return emit(brief(payload))
  emit(payload)
}

// 观测结果里几乎每次都一样的噪音字段
const DROP_KEYS = new Set([
  'interpretation',
  'output',
  'content',
  'referenceCapacity',
  'referenceMaxLifetimeSeconds',
])

function brief(value) {
  if (Array.isArray(value)) return value.map(brief)
  if (value && typeof value === 'object') {
    const out = {}
    for (const [k, v] of Object.entries(value)) {
      if (DROP_KEYS.has(k) || k === 'observation') continue
      out[k] = brief(v)
    }
    return out
  }
  return value
}

function summarizeEntries(payload) {
  const r = payload?.result ?? payload
  const entries = r?.entries
  if (!Array.isArray(entries)) {
    return { ok: payload?.ok, keys: Object.keys(payload ?? {}), note: '没有 entries 数组，用 --brief 或 --find' }
  }
  return {
    ok: payload.ok,
    total: r.total ?? entries.length,
    returned: entries.length,
    complete: r.complete,
    truncated: r.truncated,
    nextOffset: r.nextOffset ?? null,
    worldId: r.worldId,
    screen: r.screen,
    groups: r.groups?.entries?.map((g) => g.name),
    entries: entries.map((e) => {
      const row = { name: e.name, role: e.role, text: e.text }
      if (e.uiRef) row.uiRef = e.uiRef
      if (e.controlState) row.ctl = e.controlState
      if (e.targetRef) row.actionable = true
      return row
    }),
  }
}

// --find：找「标签含该文本且带引用」的节点，只投影可操作字段。
// 目的是让「找一个按钮的 targetRef」从 20 KB 输出降到 0.5 KB。
const KEEP_FOR_FIND = [
  'uiRef', 'itemRef', 'customerRef', 'name', 'role', 'text', 'targetRef', 'actionRef',
  'controlState', 'activeSelf', 'activeInHierarchy', 'itemId', 'identifier',
]

function findMatches(value, needle, out = [], depth = 0) {
  if (depth > 14 || out.length > 40) return out
  if (Array.isArray(value)) {
    for (const v of value) findMatches(v, needle, out, depth + 1)
    return out
  }
  if (value && typeof value === 'object') {
    // name 和 text 都要看：按钮的 text 可能是空串，但 name 是 "Next"
    const candidates = [value.text, value.name, value.label, value.title, value.identifier]
      .filter((v) => typeof v === 'string' && v.length > 0)
    const hasRef = value.targetRef || value.uiRef || value.itemRef || value.customerRef
    if (candidates.some((s) => s.includes(needle)) && hasRef) {
      const proj = {}
      for (const k of KEEP_FOR_FIND) if (value[k] !== undefined) proj[k] = value[k]
      out.push(proj)
    }
    for (const v of Object.values(value)) findMatches(v, needle, out, depth + 1)
  }
  return out
}

function summarizeTools(tools) {
  return (tools ?? []).map((t) => ({
    name: t.name,
    description: (t.description ?? '').split('\n')[0].slice(0, 160),
    required: t.inputSchema?.required ?? [],
    properties: Object.keys(t.inputSchema?.properties ?? {}),
  }))
}

// ---------------------------------------------------------------- 主流程

async function resolveEndpoint() {
  if (opts.url) return { url: opts.url }
  const all = loadInstances()
  if (!all.length) {
    throw new Error(
      '没有找到任何 ModDevBridgeNext 实例描述文件。\n' +
        '可能原因：游戏没启动，或游戏里「用 AI 开发 MOD」未启用。\n' +
        '检查过的目录：\n  ' + (instanceDirs().join('\n  ') || '(没有可读的 instances 目录)'),
    )
  }
  const live = []
  const rejected = []
  for (const inst of all) {
    const probe = await probeStatus(inst)
    const row = {
      ...inst,
      __url: inst.mcpUrl ?? (inst.port ? `http://127.0.0.1:${inst.port}/mcp` : null),
      __probe: probe,
    }
    if (probe.alive && row.__url) live.push(row)
    else rejected.push(row)
  }
  if (!live.length) {
    throw new Error(
      '找到实例描述文件，但没有可通过验活的端点 —— 游戏可能已经退出，或 bridge 未监听。\n' +
        rejected
          .map((x) => `- ${x.__file}\n  端点: ${x.__url ?? '(无)'}\n  原因: ${x.__probe.error ?? '端点无响应'}`)
          .join('\n'),
    )
  }
  if (opts.instance) {
    const hit = live.find((x) => x.instanceId === opts.instance)
    if (!hit) throw new Error(`--instance ${opts.instance} 未匹配到存活实例`)
    return { url: hit.__url, instance: hit }
  }
  if (live.length > 1) {
    throw new Error(
      '同时存在多个存活实例，请用 --instance 或 --url 显式指定：\n' +
        live.map((x) => `- ${x.instanceId} → ${x.__url} (${x.__file})`).join('\n'),
    )
  }
  return { url: live[0].__url, instance: live[0] }
}

async function withSession(fn) {
  const { url, instance } = await resolveEndpoint()
  const session = new McpSession(url, opts.timeout)
  const init = await session.initialize()
  try {
    return await fn(session, { url, instance, init })
  } finally {
    await session.close()
  }
}

function readArgs(spec) {
  let raw = spec ?? '{}'
  if (raw.startsWith('@')) {
    const file = raw.slice(1)
    if (!fs.existsSync(file)) throw new Error(`参数文件不存在: ${file}`)
    raw = fs.readFileSync(file, 'utf8')
  }
  try {
    return JSON.parse(raw)
  } catch (e) {
    throw new Error(`参数不是合法 JSON: ${e.message}\n原文: ${raw.slice(0, 300)}`)
  }
}

const HELP = `
ps-modbridge — ModDevBridgeNext MCP 客户端

  node mcp.mjs instances                     列出实例并验活
  node mcp.mjs status                         get_game_status
  node mcp.mjs env                            get_development_environment
  node mcp.mjs tools                          Tools/list（摘要）
  node mcp.mjs tool <name> '<json>'|@file     调一个工具
  node mcp.mjs resources                      Resources/list
  node mcp.mjs read <uri>                     Resources/read

选项：--url --instance --pretty/--compact --brief --summary --find <text>
      --save <path> --full --timeout <ms>

复杂 JSON 参数请写文件后用 @路径 传入（PowerShell 5.1 会剥掉命令行里的双引号）。
`

async function main() {
  const cmd = positional[0] ?? 'status'

  if (opts.help) {
    process.stdout.write(HELP)
    return
  }

  if (cmd === 'instances') {
    const all = loadInstances()
    if (!all.length) return emit({ instances: [], searchedDirs: instanceDirs() })
    const rows = []
    for (const inst of all) {
      const probe = await probeStatus(inst)
      rows.push({
        file: inst.__file,
        mcpUrl: inst.mcpUrl,
        instanceId: inst.instanceId,
        releaseId: inst.releaseId,
        bridgeStartedAtUtc: inst.bridgeStartedAtUtc,
        edition: inst.edition,
        alive: probe.alive,
        probedVia: probe.via,
        error: probe.error,
        status: probe.status
          ? {
              edition: probe.status.edition,
              dynamicModules: probe.status.dynamicModules,
              assemblyPath: probe.status.assemblyPath,
              disk: probe.status.disk,
            }
          : null,
      })
    }
    return emit({ searchedDirs: instanceDirs(), instances: rows })
  }

  if (cmd === 'tools') {
    return withSession(async (s) => {
      const result = await s.request('tools/list', {})
      emit({ count: result?.tools?.length ?? 0, tools: summarizeTools(result?.tools) })
    })
  }

  if (cmd === 'tool') {
    const name = positional[1]
    if (!name) throw new Error("用法: tool <name> '<json-args>' | tool <name> @args.json")
    const args = readArgs(positional[2])
    return withSession(async (s) => {
      emitCall(await s.request('tools/call', { name, arguments: args }))
    })
  }

  if (cmd === 'status') {
    return withSession(async (s) => {
      emitCall(await s.request('tools/call', { name: 'get_game_status', arguments: {} }))
    })
  }

  if (cmd === 'env') {
    return withSession(async (s) => {
      emitCall(await s.request('tools/call', { name: 'get_development_environment', arguments: {} }))
    })
  }

  if (cmd === 'resources') {
    return withSession(async (s) => emit(await s.request('resources/list', {})))
  }

  // click <文本或控件名>：一次完成「查 UI → 找控件 → 派发点击」，避免手工搬 targetRef。
  // 命中多个时拒绝猜测（--index 指定，--force 取第一个），这是防止误点的关键。
  if (cmd === 'click') {
    const needle = positional[1]
    if (!needle) throw new Error('用法: click <屏幕文本或控件名> [--index n] [--force]')
    return withSession(async (s) => {
      const q = await s.request('tools/call', {
        name: 'query_ui',
        arguments: { limit: 100, timeout: 25 },
      })
      const payload = q?.structuredContent ?? q
      if (!payload?.ok) return emit(payload)
      const r = payload.result
      // worldId 可能只在 envelope 上（result 未带），两个位置都要看
      const worldId = r?.worldId ?? payload.worldId
      if (!worldId) throw new Error('query_ui 没有返回 worldId，无法派发点击；先跑 status 确认游戏状态')
      const entries = r?.entries ?? []
      const hits = entries.filter((e) => {
        const cands = [e.text, e.name].filter((x) => typeof x === 'string' && x.length)
        return cands.some((x) => x.includes(needle)) && e.targetRef
      })
      const usable = hits.filter((e) => e.controlState?.interactable !== false)
      const pool = usable.length ? usable : hits
      if (!pool.length) {
        throw new Error(
          `没找到可点击的「${needle}」。当前可见控件：\n` +
            entries.map((e) => `- ${e.name} | ${e.text ?? ''}`).join('\n'),
        )
      }
      if (pool.length > 1 && opts.index === undefined && !opts.force) {
        throw new Error(
          `「${needle}」命中 ${pool.length} 个可点击控件，请用 --index 选择：\n` +
            pool.map((e, i) => `  [${i}] ${e.name} | ${e.text ?? ''}`).join('\n'),
        )
      }
      const pick = pool[opts.index ?? 0]
      const res = await s.request('tools/call', {
        name: 'operate_ui',
        arguments: { world_id: worldId, action: 'click', target_ref: pick.targetRef, timeout: 25 },
      })
      const out = res?.structuredContent ?? res
      const d = out?.result
      emit({
        clicked: { name: pick.name, text: pick.text ?? '' },
        ok: out?.ok,
        dispatched: d
          ? { events: d.events, released: d.released, completedDispatch: d.completedDispatch, worldChanged: d.worldChangedDuringDispatch }
          : null,
        error: out?.error ?? null,
        executionState: out?.executionState ?? null,
      })
    })
  }

  if (cmd === 'read') {
    const uri = positional[1]
    if (!uri) throw new Error('用法: read <uri>')
    return withSession(async (s) => emit(await s.request('resources/read', { uri })))
  }

  if (cmd === 'raw') {
    const payload = readArgs(positional[1])
    if (!payload?.method) throw new Error('raw 需要 {method, params?}')
    return withSession(async (s) => emit(await s.request(payload.method, payload.params)))
  }

  throw new Error(`未知子命令: ${cmd}（--help 看用法）`)
}

main().catch((e) => {
  process.stderr.write(`✗ ${e.message}\n`)
  process.exitCode = 1
})
