import { afterEach, beforeEach, expect, test } from "bun:test";
import {
  advanceReaderSpring,
  createReaderSheetMotion,
} from "../../src/features/knowledge/reader-sheet-motion";
import { createSpring } from "../../src/lib/spring-motion";
import { createFilterDisclosureMotion } from "../../src/features/knowledge/filter-disclosure-motion";
import { createSearchDialogMotion } from "../../src/features/knowledge/search-dialog-motion";
import { createPreviewSwitchMotion } from "../../src/features/knowledge/preview-switch-motion";

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

test("preview changes keep the current opacity and scroll position until new content arrives", () => {
  const parent = { scrollTop: 0 };
  const node = { style: { opacity: "" }, parentElement: parent };
  const controller = createPreviewSwitchMotion(node as unknown as HTMLElement);
  try {
    controller.update(false, "document:a");
    parent.scrollTop = 420;
    controller.update(true);
    tick(32);
    const before = node.style.opacity;
    expect(Number(before)).toBeLessThan(1);
    expect(parent.scrollTop).toBe(420);
    controller.update(false, "document:b");
    expect(node.style.opacity).toBe(before);
    expect(parent.scrollTop).toBe(0);
    tick(16);
    expect(Number(node.style.opacity)).toBeLessThan(Number(before));
    tick(1000);
    expect(node.style.opacity).toBe("1");
    parent.scrollTop = 100;
    controller.update(false, "document:b");
    expect(parent.scrollTop).toBe(100);
  } finally {
    controller.dispose();
  }
});

test("preview motion respects reduced motion immediately and cleans up interrupted frames", () => {
  const node = { style: { opacity: "" } } as unknown as HTMLElement;
  const controller = createPreviewSwitchMotion(node);
  try {
    controller.update(true);
    tick(16);
    preference.matches = true;
    preference.dispatchEvent(new Event("change"));
    expect(node.style.opacity).toBe("0.2");
    controller.update(false, "document:b");
    expect(node.style.opacity).toBe("1");
    expect(callbacks.size).toBe(0);
    preference.matches = false;
    controller.update(true);
    controller.dispose();
    tick(1000);
    expect(callbacks.size).toBe(0);
  } finally {
    controller.dispose();
  }
});

test("preview loading fades reverse from their current value and preserve velocity", () => {
  const node = { style: { opacity: "" } } as unknown as HTMLElement;
  const status = { style: { opacity: "" } } as unknown as HTMLElement;
  const controller = createPreviewSwitchMotion(node, status);
  try {
    expect(status.style.opacity).toBe("0");
    controller.update(false, undefined, true);
    tick(32);
    const entering = Number(status.style.opacity);
    expect(entering).toBeGreaterThan(0);
    expect(entering).toBeLessThan(1);
    expect(node.style.opacity).toBe("1");
    controller.update(false, "document:a", false);
    expect(Number(status.style.opacity)).toBe(entering);
    tick(16);
    expect(Number(status.style.opacity)).toBeGreaterThan(entering);
    tick(1000);
    expect(status.style.opacity).toBe("0");
    controller.update(true, undefined, true);
    tick(1000);
    expect(status.style.opacity).toBe("1");
    expect(node.style.opacity).toBe("0.2");
    controller.update(false, "document:b", false);
    controller.dispose();
    tick(1000);
    expect(callbacks.size).toBe(0);
  } finally {
    controller.dispose();
  }
});

test("preview loading remains static with reduced motion and hides on completion", () => {
  preference.matches = true;
  const node = { style: { opacity: "" } } as unknown as HTMLElement;
  const status = { style: { opacity: "" } } as unknown as HTMLElement;
  const controller = createPreviewSwitchMotion(node, status);
  try {
    controller.update(true);
    expect(status.style.opacity).toBe("1");
    controller.update(false, "document:a");
    expect(status.style.opacity).toBe("0");
    expect(node.style.opacity).toBe("1");
    expect(callbacks.size).toBe(0);
  } finally {
    controller.dispose();
  }
});

function searchDialogSurface() {
  let measurements = 0;
  let expandedHeight = 620;
  const compact = {
    get offsetHeight() {
      measurements++;
      return 400;
    },
  };
  const expanded = {
    get offsetHeight() {
      measurements++;
      return expandedHeight;
    },
  };
  const node = {
    style: { height: "", opacity: "", transform: "" },
    querySelector: (selector: string) =>
      selector.includes("compact") ? compact : expanded,
  };
  return {
    node,
    surface: node as unknown as HTMLElement,
    measurements: () => measurements,
    resize: (value: number) => {
      expandedHeight = value;
    },
  };
}

test("search dialog resize reverses from its current height and keeps layout reads off animation frames", () => {
  const dialog = searchDialogSurface();
  const controller = createSearchDialogMotion(
    dialog.surface,
    overlay,
    false,
    () => closed++,
  );
  try {
    controller.setExpanded(true);
    const reads = dialog.measurements();
    tick(32);
    const before = parseFloat(dialog.node.style.height);
    expect(before).toBeGreaterThan(400);
    expect(before).toBeLessThan(620);
    expect(dialog.measurements()).toBe(reads);
    controller.setExpanded(false);
    expect(parseFloat(dialog.node.style.height)).toBe(before);
    tick(16);
    expect(parseFloat(dialog.node.style.height)).toBeGreaterThan(before);
    controller.setExpanded(true);
    tick(1000);
    expect(dialog.node.style.height).toBe("620px");
    expect(dialog.node.style.opacity).toBe("1");
    expect(closed).toBe(0);
  } finally {
    controller.dispose();
  }
});

test("search dialog reopening cancels dismissal and restores the same surface and overlay", () => {
  const dialog = searchDialogSurface();
  const controller = createSearchDialogMotion(
    dialog.surface,
    overlay,
    false,
    () => closed++,
  );
  try {
    tick(1000);
    controller.close();
    tick(32);
    const opacity = dialog.node.style.opacity;
    controller.open();
    expect(dialog.node.style.opacity).toBe(opacity);
    tick(1000);
    expect(dialog.node.style.opacity).toBe("1");
    expect(overlay.style.opacity).toBe("1");
    expect(closed).toBe(0);
    controller.close();
    tick(1000);
    expect(closed).toBe(1);
  } finally {
    controller.dispose();
  }
});

test("search dialog viewport changes respect reduced motion and disposal cancels pending work", () => {
  let resize = () => {};
  Object.defineProperty(globalThis, "ResizeObserver", {
    configurable: true,
    value: class {
      constructor(callback: () => void) {
        resize = callback;
      }
      observe() {}
      disconnect() {}
    },
  });
  preference.matches = true;
  const dialog = searchDialogSurface();
  const controller = createSearchDialogMotion(
    dialog.surface,
    overlay,
    true,
    () => closed++,
  );
  try {
    expect(dialog.node.style.height).toBe("620px");
    expect(dialog.node.style.opacity).toBe("1");
    dialog.resize(500);
    resize();
    expect(dialog.node.style.height).toBe("500px");
    controller.setExpanded(false);
    expect(dialog.node.style.height).toBe("400px");
    expect(callbacks.size).toBe(0);
    preference.matches = false;
    controller.setExpanded(true);
    controller.close();
    controller.dispose();
    resize();
    tick(1000);
    expect(closed).toBe(0);
    expect(callbacks.size).toBe(0);
  } finally {
    controller.dispose();
  }
});

test("filter disclosure can reverse mid-flight without measuring layout on every frame", () => {
  let measurements = 0;
  const node = {
    hidden: false,
    style: { height: "", opacity: "" },
    firstElementChild: {
      getBoundingClientRect: () => {
        measurements++;
        return { height: 240 };
      },
    },
    getBoundingClientRect: () => ({
      height:
        node.style.height === "auto" ? 240 : parseFloat(node.style.height),
    }),
  };
  const disclosure = createFilterDisclosureMotion(
    node as unknown as HTMLDivElement,
    false,
  );
  try {
    expect(node.hidden).toBe(true);
    disclosure.setOpen(true);
    const measured = measurements;
    tick(32);
    tick(16);
    expect(measurements).toBe(measured);
    const current = node.style.height;
    disclosure.setOpen(false);
    expect(node.style.height).toBe(current);
    tick(16);
    disclosure.setOpen(true);
    tick(2000);
    expect(node.hidden).toBe(false);
    expect(node.style.height).toBe("auto");
    disclosure.setOpen(false);
    tick(2000);
    expect(node.hidden).toBe(true);
    expect(node.style.opacity).toBe("0");
  } finally {
    disclosure.dispose();
  }
});

test("filter disclosure starts from changed auto height and reduced motion settles synchronously", () => {
  let contentHeight = 240;
  const node = {
    hidden: false,
    style: { height: "", opacity: "" },
    firstElementChild: {
      getBoundingClientRect: () => ({ height: contentHeight }),
    },
    getBoundingClientRect: () => ({
      height:
        node.style.height === "auto"
          ? contentHeight
          : parseFloat(node.style.height),
    }),
  };
  const disclosure = createFilterDisclosureMotion(
    node as unknown as HTMLDivElement,
    true,
  );
  try {
    contentHeight = 400;
    disclosure.setOpen(false);
    expect(node.style.height).toBe("400px");
    preference.matches = true;
    preference.dispatchEvent(new Event("change"));
    expect(node.hidden).toBe(true);
    expect(callbacks.size).toBe(0);
    disclosure.setOpen(true);
    expect(node.hidden).toBe(false);
    expect(node.style.height).toBe("auto");
    expect(callbacks.size).toBe(0);
  } finally {
    disclosure.dispose();
  }
});
