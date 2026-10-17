/** An exact critically damped step, independent of the display's frame rate. */
export function advanceSpring(
  position: number,
  velocity: number,
  target: number,
  seconds: number,
) {
  const frequency = (2 * Math.PI) / 0.3;
  const displacement = position - target;
  const impulse = velocity + frequency * displacement;
  const decay = Math.exp(-frequency * seconds);
  return {
    position: target + (displacement + impulse * seconds) * decay,
    velocity: (velocity - frequency * impulse * seconds) * decay,
  };
}

export function createSpring(initial: number, paint: (value: number) => void) {
  let position = initial,
    velocity = 0,
    target = initial,
    frame = 0;
  let previous = performance.now();
  let onRest: (() => void) | undefined;
  let disposed = false;
  const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
  function advance(time: number) {
    ({ position, velocity } = advanceSpring(
      position,
      velocity,
      target,
      Math.max(0, time - previous) / 1000,
    ));
    previous = time;
  }
  function settle() {
    position = target;
    velocity = 0;
    paint(position);
    const done = onRest;
    onRest = undefined;
    done?.();
  }
  function tick(time: number) {
    if (disposed) return;
    frame = 0;
    advance(time);
    paint(position);
    if (Math.abs(position - target) < 0.001 && Math.abs(velocity) < 0.02)
      settle();
    else frame = requestAnimationFrame(tick);
  }
  function preference() {
    if (reduced.matches) {
      cancelAnimationFrame(frame);
      frame = 0;
      settle();
    }
  }
  reduced.addEventListener("change", preference);
  paint(position);
  return {
    to(next: number, done?: () => void) {
      if (disposed) return;
      advance(performance.now());
      target = next;
      onRest = done;
      cancelAnimationFrame(frame);
      frame = 0;
      if (reduced.matches || (position === target && velocity === 0)) settle();
      else frame = requestAnimationFrame(tick);
    },
    dispose() {
      disposed = true;
      cancelAnimationFrame(frame);
      reduced.removeEventListener("change", preference);
      onRest = undefined;
    },
  };
}
