import { afterEach, beforeEach, expect, test } from "bun:test";
import { changeDisplay } from "../../src/lib/display-transition";

const documentDescriptor = Object.getOwnPropertyDescriptor(
  globalThis,
  "document",
);
const windowDescriptor = Object.getOwnPropertyDescriptor(globalThis, "window");
let frames: Keyframe[][];
let cancellations: number;
let reduced: boolean;
let region: HTMLElement;
beforeEach(() => {
  frames = [];
  cancellations = 0;
  reduced = false;
  region = {
    animate(keyframes: Keyframe[]) {
      frames.push(keyframes);
      return {
        playState: "running",
        finished: new Promise(() => {}),
        cancel() {
          cancellations++;
        },
      };
    },
  } as unknown as HTMLElement;
  Object.defineProperty(globalThis, "document", {
    configurable: true,
    value: { querySelector: () => region },
  });
  Object.defineProperty(globalThis, "window", {
    configurable: true,
    value: {
      matchMedia: () => ({ matches: reduced }),
      getComputedStyle: () => ({ opacity: "0.84" }),
    },
  });
});
afterEach(() => {
  reduced = true;
  changeDisplay(region, () => {});
  for (const [name, descriptor] of [
    ["document", documentDescriptor],
    ["window", windowDescriptor],
  ] as const) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor);
    else Reflect.deleteProperty(globalThis, name);
  }
});

test("rapid choices commit immediately and animate from the interrupted visual state", () => {
  let selected = "list";
  changeDisplay(region, () => {
    selected = "cards";
  });
  expect(selected).toBe("cards");
  changeDisplay(region, () => {
    selected = "list";
  });
  expect(selected).toBe("list");
  expect(cancellations).toBe(1);
  expect(frames[1]?.[0]?.opacity).toBe("0.84");
});

test("reduced motion cancels an active animation and commits without scheduling another", () => {
  let selected = "list";
  changeDisplay(region, () => {
    selected = "cards";
  });
  reduced = true;
  changeDisplay(region, () => {
    selected = "list";
  });
  expect(selected).toBe("list");
  expect(cancellations).toBe(1);
  expect(frames).toHaveLength(1);
});

test("independent content surfaces do not cancel each other's animation", () => {
  const second = { animate: region.animate } as HTMLElement;
  changeDisplay(region, () => {});
  changeDisplay(second, () => {});
  expect(cancellations).toBe(0);
  expect(frames).toHaveLength(2);
  changeDisplay(region, () => {});
  expect(cancellations).toBe(1);
});

test("a missing surface still commits the user's choice", () => {
  let selected = "list";
  changeDisplay(null, () => {
    selected = "cards";
  });
  expect(selected).toBe("cards");
  expect(frames).toHaveLength(0);
});
