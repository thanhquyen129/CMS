"use client";

import { useRef } from "react";
import { newIdempotencyKey } from "@/lib/idempotency";

/**
 * One in-flight money submit, one key until it succeeds.
 * A second click in the same turn is ignored. A failed request keeps the key so retry does not insert a second row.
 */
export function useIdempotency(prefix: string) {
  const lock = useRef(false);
  const key = useRef<string | null>(null);

  return {
    acquire(): string | null {
      if (lock.current) return null;
      lock.current = true;
      if (!key.current) key.current = newIdempotencyKey(prefix);
      return key.current;
    },
    release(succeeded: boolean) {
      lock.current = false;
      if (succeeded) key.current = null;
    },
  };
}
