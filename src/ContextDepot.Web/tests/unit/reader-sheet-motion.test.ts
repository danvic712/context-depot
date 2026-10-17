import { afterEach, beforeEach, expect, test } from "bun:test";
import {
  advanceReaderSpring,
  createReaderSheetMotion,
} from "../../src/features/knowledge/reader-sheet-motion";
import { createSpring } from "../../src/lib/spring-motion";

const names = [
  "window",
  "performance",
  "requestAnimationFrame",
  "cancelAnimationFrame",
  "ResizeObserver",
] as const;
const originals = new Map(
  names.map((name) => [
    name,
    Object.getOwnPropertyDescriptor(globalThis, name),
  ]),
);
let time: number;
let serial: number;
let callbacks: Map<number, FrameRequestCallback>;
let preference: EventTarget & { matches: boolean };
let surface: HTMLElement;
let overlay: HTMLElement;
let motion: ReturnType<typeof createReaderSheetMotion> | undefined;
let closed: number;
function tick(milliseconds: number) {
  time += milliseconds;
  const pending = [...callbacks.values()];
  callbacks.clear();
  for (const callback of pending) callback(time);
}
function position() {
  return Number(surface.style.transform.match(/translate3d\(([-\d.]+)/)![1]);
}
beforeEach(() => {
  time = 0;
  serial = 0;
  closed = 0;
  callbacks = new Map();
  preference = Object.assign(new EventTarget(), { matches: false });
  surface = {
    style: {},
    getBoundingClientRect: () => ({ width: 400 }),
  } as unknown as HTMLElement;
  overlay = { style: {} } as HTMLElement;
  const values = {
    window: { matchMedia: () => preference },
    performance: { now: () => time },
    requestAnimationFrame: (callback: FrameRequestCallback) => {
      callbacks.set(++serial, callback);
      return serial;
    },
    cancelAnimationFrame: (id: number) => {
      callbacks.delete(id);
    },
    ResizeObserver: class {
      observe() {}
      disconnect() {}
    },
  };
  for (const name of names)
    Object.defineProperty(globalThis, name, {
      configurable: true,
      value: values[name],
    });
});
afterEach(() => {
  motion?.dispose();
  motion = undefined;
  for (const name of names) {
    const descriptor = originals.get(name);
    if (descriptor) Object.defineProperty(globalThis, name, descriptor);
    else Reflect.deleteProperty(globalThis, name);
  }
});

test("critical damping settles without overshoot and is independent of frame rate", () => {
  const single = advanceReaderSpring(400, 0, 0, 0.16);
  let split = { position: 400, velocity: 0 };
  for (let i = 0; i < 10; i++) {
    const next = advanceReaderSpring(split.position, split.velocity, 0, 0.016);
    expect(next.position).toBeGreaterThanOrEqual(0);
    expect(next.position).toBeLessThan(split.position);
    split = next;
  }
  expect(split.position).toBeCloseTo(single.position, 8);
  expect(split.velocity).toBeCloseTo(single.velocity, 8);
});
test("reversing mid-flight keeps the current position and carries velocity into closing", () => {
  motion = createReaderSheetMotion(surface, overlay, () => closed++);
  tick(32);
  const before = position();
  motion.close();
  expect(position()).toBe(before);
  expect(closed).toBe(0);
  tick(16);
  expect(position()).toBeLessThan(before); // Momentum continues before turning toward the new target.
  tick(1000);
  expect(position()).toBe(400);
  expect(overlay.style.opacity).toBe("0");
  expect(closed).toBe(1);
  expect(callbacks.size).toBe(0);
});
test("a navigation during dismissal reverses the same sheet without running the close callback", () => {
  motion = createReaderSheetMotion(surface, overlay, () => closed++);
  tick(1000);
  motion.close();
  tick(48);
  const before = position();
  motion.open();
  expect(position()).toBe(before);
  tick(1000);
  expect(position()).toBe(0);
  expect(closed).toBe(0);
  expect(overlay.style.opacity).toBe("1");
});
test("reduced motion changes stop an active spring and dismissal never waits on a frame", () => {
  motion = createReaderSheetMotion(surface, overlay, () => closed++);
  tick(16);
  preference.matches = true;
  preference.dispatchEvent(new Event("change"));
  expect(position()).toBe(0);
  expect(callbacks.size).toBe(0);
  motion.close();
  expect(closed).toBe(1);
  expect(callbacks.size).toBe(0);
});
test("unmount cancels pending frames without firing a stale close callback", () => {
  motion = createReaderSheetMotion(surface, overlay, () => closed++);
  motion.close();
  motion.dispose();
  tick(1000);
  expect(closed).toBe(0);
  expect(callbacks.size).toBe(0);
});

test("shared surface springs reverse from their current position and discard replaced completions", () => {
  let value = 1,
    finished = 0;
  const spring = createSpring(1, (next) => {
    value = next;
  });
  try {
    spring.to(0, () => {
      finished++;
    });
    tick(32);
    const current = value;
    spring.to(1);
    expect(value).toBe(current);
    tick(16);
    expect(value).toBeLessThan(current);
    tick(1000);
    expect(value).toBe(1);
    expect(finished).toBe(0);
  } finally {
    spring.dispose();
  }
});
test("shared surface springs apply reduced motion immediately and ignore targets after disposal", () => {
  preference.matches = true;
  let value = 0,
    finished = 0;
  const spring = createSpring(0, (next) => {
    value = next;
  });
  spring.to(1, () => {
    finished++;
  });
  expect(value).toBe(1);
  expect(finished).toBe(1);
  expect(callbacks.size).toBe(0);
  spring.dispose();
  spring.to(0);
  expect(value).toBe(1);
  expect(callbacks.size).toBe(0);
});
