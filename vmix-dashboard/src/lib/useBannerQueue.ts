import { useCallback, useEffect, useRef, useState } from "react";

// One-at-a-time banners (ELIMINATED, FIRST BLOOD...). PMGO never stacks or cuts off a banner:
// when two teams are wiped within a second, the second waits for the first to finish. Before
// this, a new banner simply replaced the one on screen, so near-simultaneous eliminations were
// only half-shown.
//
// - `dedupeKey`: the same key within 15 s is dropped (the real backend event and the overlay's
//   own derived fallback can both report the same wipe).
// - the queue is capped, so a burst can't leave the overlay replaying stale banners for a minute.

export interface QueueItem { id: string; dedupeKey?: string }

const DEDUPE_WINDOW_MS = 15000;
const MAX_WAITING = 4;
const EXIT_MS = 380;

export function useBannerQueue<T extends QueueItem>(showMs: number) {
  const [current, setCurrent] = useState<T | null>(null);
  const [visible, setVisible] = useState(false);
  const queue = useRef<T[]>([]);
  const recent = useRef(new Map<string, number>());
  const busy = useRef(false);
  const timers = useRef<number[]>([]);

  const later = (fn: () => void, ms: number) => { timers.current.push(window.setTimeout(fn, ms)); };

  const showNextRef = useRef<() => void>(() => {});
  showNextRef.current = () => {
    const next = queue.current.shift();
    if (!next) { busy.current = false; setCurrent(null); return; }
    busy.current = true;
    setCurrent(next);
    setVisible(false);
    // one frame at the "before" state so the entrance transition actually plays
    later(() => setVisible(true), 30);
    later(() => {
      setVisible(false);
      later(() => showNextRef.current(), EXIT_MS);
    }, showMs);
  };

  const push = useCallback((item: T) => {
    if (item.dedupeKey) {
      const now = Date.now();
      const seen = recent.current.get(item.dedupeKey);
      if (seen !== undefined && now - seen < DEDUPE_WINDOW_MS) return;
      recent.current.set(item.dedupeKey, now);
    }
    queue.current.push(item);
    if (queue.current.length > MAX_WAITING) queue.current.splice(0, queue.current.length - MAX_WAITING);
    if (!busy.current) showNextRef.current();
  }, []);

  useEffect(() => () => { timers.current.forEach((t) => window.clearTimeout(t)); }, []);

  return { current, visible, push };
}
