// Configuration contract checks only; these do not execute Codex or an MCP connection.
// Run: node --test tests/hooks/native-mcp-hook.test.mjs
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import test from 'node:test';

const config = JSON.parse(readFileSync(new URL('../../scripts/codex/hooks.example.json', import.meta.url), 'utf8'));
const eventNames = ['UserPromptSubmit', 'Stop'];
const handler = event => config.hooks[event][0].hooks[0];

function placeholders(value) {
  if (typeof value === 'string') return [...value.matchAll(/\$\{([^}]+)\}/g)].map(match => match[1]);
  if (Array.isArray(value)) return value.flatMap(placeholders);
  if (value && typeof value === 'object') return Object.values(value).flatMap(placeholders);
  return [];
}

test('both lifecycle events call only the existing native MCP capture tool', () => {
  assert.deepEqual(Object.keys(config), ['hooks']);
  assert.deepEqual(Object.keys(config.hooks), eventNames);
  for (const event of eventNames) {
    assert.equal(config.hooks[event].length, 1);
    assert.deepEqual(Object.keys(config.hooks[event][0]), ['hooks']);
    assert.equal(config.hooks[event][0].hooks.length, 1);
    const { input, ...target } = handler(event);
    assert.deepEqual(target, {
      type: 'mcp_tool', server: 'context-depot', tool: 'session_capture', timeout: 5,
    });
    assert.equal(typeof input, 'object');
  }
});

test('capture arguments opt in to schema 2 auto without workspace or managed lifecycle', () => {
  for (const event of eventNames) {
    const { clientSessionId, turnId, eventId, messages, ...protocol } = handler(event).input;
    assert.deepEqual(protocol, { schemaVersion: 2, agent: 'codex', workspaceMode: 'auto', mode: 'message' });
    assert.equal(clientSessionId, '${session_id}');
    assert.equal(turnId, '${turn_id}');
    assert.equal(typeof eventId, 'string');
    assert.equal(messages.length, 1);
  }
});

test('user and assistant bodies are full typed placeholders with distinct roles', () => {
  assert.deepEqual(handler('UserPromptSubmit').input.messages, [{ role: 'user', content: '${prompt}' }]);
  assert.deepEqual(handler('Stop').input.messages, [{ role: 'assistant', content: '${last_assistant_message}' }]);
  // A full placeholder (rather than surrounding text) is required for Codex to retain JSON null.
  for (const event of eventNames) assert.match(handler(event).input.messages[0].content, /^\$\{[^}]+\}$/);
});

test('event identities depend on turn and phase but never message content', () => {
  assert.equal(handler('UserPromptSubmit').input.eventId, 'codex:${turn_id}:user');
  assert.equal(handler('Stop').input.eventId, 'codex:${turn_id}:assistant');
  assert.notEqual(handler('UserPromptSubmit').input.eventId, handler('Stop').input.eventId);
  for (const event of eventNames) {
    assert.deepEqual(placeholders(handler(event).input.eventId), ['turn_id']);
  }
});

test('only native correlation and visible message fields are read from the event', () => {
  assert.deepEqual([...new Set(placeholders(handler('UserPromptSubmit').input))].sort(), ['prompt', 'session_id', 'turn_id']);
  assert.deepEqual([...new Set(placeholders(handler('Stop').input))].sort(), ['last_assistant_message', 'session_id', 'turn_id']);
  for (const event of eventNames) {
    assert.deepEqual(Object.keys(handler(event)).sort(), ['input', 'server', 'timeout', 'tool', 'type']);
    assert.deepEqual(Object.keys(handler(event).input).sort(), [
      'agent', 'clientSessionId', 'eventId', 'messages', 'mode', 'schemaVersion', 'turnId', 'workspaceMode',
    ]);
  }
});

test('the Python bridge and its obsolete transport suite are no longer shipped', () => {
  assert.equal(existsSync(new URL('../../scripts/codex/session-memory-hook.py', import.meta.url)), false);
  assert.equal(existsSync(new URL('test_codex_session_memory_hook.py', import.meta.url)), false);
});
