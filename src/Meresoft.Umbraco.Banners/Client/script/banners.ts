// Package script (server mode): reports banners that were really seen. No dependencies, no cookies, nothing stored.
// A banner counts after 1,000 ms of continuous visibility at 50% or more while the tab is visible (FR-011).
// Without IntersectionObserver nothing is counted. The server decides what counts; see contracts/public-endpoints.md.
// Client mode (cached pages): a placeholder asks {base}/zone/{alias}, gets public fields, builds the banners through
// the DOM and then treats them like server mode banners.
(() => {
  if (!("IntersectionObserver" in window)) {
    return;
  }

  interface Delivered {
    id: number;
    type: string;
    clickUrl?: string;
    newWindow?: boolean;
    imageUrl?: string;
    width?: number | null;
    height?: number | null;
    alt?: string;
    title?: string | null;
    html?: string;
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

  // Elements are built with setAttribute, so a value is never parsed as markup. Null and undefined mean "no attribute".
  const make = (tag: string, attrs: Record<string, string | number | null | undefined>, ...children: Element[]): HTMLElement => {
    const el = document.createElement(tag);
    for (const name of Object.keys(attrs)) {
      const value = attrs[name];
      if (value != null) {
        el.setAttribute(name, String(value));
      }
    }
    el.append(...children);
    return el;
  };

  const build = (b: Delivered): HTMLElement => {
    if (b.type === "html") {
      const box = make("div", { class: "mb-banner mb-html", "data-mbb": b.id });
      // Scripts that come in through innerHTML never run, so each one is replaced by a fresh copy.
      box.innerHTML = b.html ?? "";
      box.querySelectorAll("script").forEach((old) => {
        const script = document.createElement("script");
        old.getAttributeNames().forEach((name) => script.setAttribute(name, old.getAttribute(name) ?? ""));
        script.textContent = old.textContent;
        old.replaceWith(script);
      });
      return box;
    }
    return make(
      "div",
      { class: "mb-banner", "data-mbb": b.id },
      make(
        "a",
        { href: b.clickUrl, rel: "sponsored noopener", target: b.newWindow ? "_blank" : null, title: b.title },
        make("img", { src: b.imageUrl, alt: b.alt, width: b.width, height: b.height, loading: "lazy", decoding: "async" }),
      ),
    );
  };

  // The placeholder keeps its fallback until at least one banner has come back; any failure leaves it as it is.
  const load = (zone: HTMLElement): void => {
    const { mbBase, mbz } = zone.dataset;
    if (!mbBase || !mbz) {
      return;
    }
    fetch(mbBase + "/zone/" + encodeURIComponent(mbz), { credentials: "omit" })
      .then((r) => (r.ok ? r.json() : null))
      .then((d: { token?: string; banners?: Delivered[] } | null) => {
        // The answer comes from the site's own origin and is trusted, as the Html banner markup is (FR-006). As defence
        // in depth an image banner only links through the click endpoint of this site, never to another address.
        const els = (d?.banners ?? []).filter((b) => b.type === "html" || b.clickUrl?.startsWith(mbBase + "/c/")).map(build);
        if (d?.token && els.length) {
          zone.dataset.mbzToken = d.token;
          zone.replaceChildren(...els);
          els.forEach((el) => observer.observe(el));
        }
      })
      .catch(() => {});
  };

  document.querySelectorAll<HTMLElement>("[data-mbz-mode=client]").forEach(load);
})();

// Makes the file a module so the tests can import it; the build drops this line.
export {};
