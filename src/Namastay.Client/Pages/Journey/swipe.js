// Drag runs here, not in Elmish, so a swipe never waits for a render.
// Values come from DESIGN.md.
const DISTANCE = 110;
const FLICK = 0.45;
const SPRING_MS = 320;
const EXIT_MS = 240;
const ROTATION = 0.06;
const EASE_OUT = "cubic-bezier(.23,1,.32,1)";

const prefersStillness = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;

const offscreen = (direction) => direction * (window.innerWidth + 200);

function place(element, x) {
  element.style.transform = `translateX(${x}px) rotate(${x * ROTATION}deg)`;
}

/** Plays the exit, then reports the answer once. */
export function leave(element, liked, onDecide) {
  const direction = liked ? 1 : -1;
  const frames = prefersStillness()
    ? [{ opacity: 1 }, { opacity: 0 }]
    : [
        { transform: element.style.transform || "none", opacity: 1 },
        { transform: `translateX(${offscreen(direction)}px) rotate(${direction * 18}deg)`, opacity: 1 },
      ];

  const exit = element.animate(frames, { duration: EXIT_MS, easing: EASE_OUT, fill: "forwards" });
  exit.onfinish = () => onDecide(liked);
}

function springBack(element) {
  const from = element.style.transform;
  element.style.transform = "";
  element.animate([{ transform: from }, { transform: "none" }], { duration: SPRING_MS, easing: EASE_OUT });
}

export function attachSwipe(element, onDecide) {
  let drag = null;
  let decided = false;

  const down = (event) => {
    if (drag || decided) return;
    drag = { id: event.pointerId, startX: event.clientX, startAt: performance.now(), x: 0 };
    element.setPointerCapture(event.pointerId);
  };

  const move = (event) => {
    if (!drag || event.pointerId !== drag.id) return;
    drag.x = event.clientX - drag.startX;
    if (!prefersStillness()) place(element, drag.x);
  };

  const up = (event) => {
    if (!drag || event.pointerId !== drag.id) return;
    const { x, startAt } = drag;
    const speed = Math.abs(x) / Math.max(performance.now() - startAt, 1);
    drag = null;

    if (Math.abs(x) >= DISTANCE || speed > FLICK) {
      decided = true;
      leave(element, x > 0, onDecide);
    } else if (!prefersStillness()) {
      springBack(element);
    }
  };

  const cancel = () => {
    if (!drag) return;
    drag = null;
    springBack(element);
  };

  element.addEventListener("pointerdown", down);
  element.addEventListener("pointermove", move);
  element.addEventListener("pointerup", up);
  element.addEventListener("pointercancel", cancel);

  return () => {
    element.removeEventListener("pointerdown", down);
    element.removeEventListener("pointermove", move);
    element.removeEventListener("pointerup", up);
    element.removeEventListener("pointercancel", cancel);
  };
}
