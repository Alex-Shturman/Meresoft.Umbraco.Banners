// Package script (server mode): reports banners that were really seen. No dependencies, no cookies, nothing stored.
// A banner counts after 1,000 ms of continuous visibility at 50% or more while the tab is visible (FR-011).
// Without IntersectionObserver nothing is counted. The server decides what counts; see contracts/public-endpoints.md.
(() => {
  if (!("IntersectionObserver" in window)) {
    return;
  }

  const HOLD_MS = 1000;
  const SEND_EVERY_MS = 2000;
  const MAX_LINES = 50;
  const queue: Record<string, string[]> = {};
  const timers = new Map<Element, number>();
  const inView = new Set<Element>();
  let sendTimer = 0;

  const send = (): void => {
    sendTimer = 0;
    for (const base of Object.keys(queue)) {
      const lines = queue[base];
      delete queue[base];
      const url = base + "/seen";
      // The server reads at most 50 lines of a request, so a long queue goes out in parts.
      for (let from = 0; from < lines.length; from += MAX_LINES) {
        const body = lines.slice(from, from + MAX_LINES).join("\n");
        if (!(navigator.sendBeacon && navigator.sendBeacon(url, body))) {
          fetch(url, { method: "POST", body, keepalive: true, credentials: "omit" }).catch(() => {});
        }
      }
    }
  };

  const report = (el: Element): void => {
    observer.unobserve(el); // at most one report per element and page view
    inView.delete(el);
    const zone = el.closest<HTMLElement>("[data-mbz]");
    const base = zone?.dataset.mbBase;
    const token = zone?.dataset.mbzToken;
    if (base && token) {
      (queue[base] ??= []).push(token + " " + (el as HTMLElement).dataset.mbb);
      sendTimer ||= window.setTimeout(send, SEND_EVERY_MS);
    }
  };

  const start = (el: Element): void => {
    if (!timers.has(el)) {
      timers.set(
        el,
        window.setTimeout(() => {
          timers.delete(el);
          report(el);
        }, HOLD_MS),
      );
    }
  };

  const stop = (el: Element): void => {
    clearTimeout(timers.get(el));
    timers.delete(el);
  };

  const observer = new IntersectionObserver(
    (entries) => {
      for (const { target, isIntersecting, intersectionRatio } of entries) {
        if (isIntersecting && intersectionRatio >= 0.5) {
          inView.add(target);
          if (document.visibilityState === "visible") {
            start(target);
          }
        } else {
          inView.delete(target);
          stop(target);
        }
      }
    },
    { threshold: [0.5] },
  );

  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible") {
      inView.forEach(start);
    } else {
      [...timers.keys()].forEach(stop); // a hidden tab resets every timer
      send();
    }
  });
  addEventListener("pagehide", send);

  document.querySelectorAll("[data-mbz][data-mbz-token] [data-mbb]").forEach((el) => observer.observe(el));
})();

// Makes the file a module so the tests can import it; the build drops this line.
export {};
