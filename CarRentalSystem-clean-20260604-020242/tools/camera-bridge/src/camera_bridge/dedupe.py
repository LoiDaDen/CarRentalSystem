from __future__ import annotations

import time


class DedupeCache:
    def __init__(self, ttl_seconds: int) -> None:
        self.ttl_seconds = max(ttl_seconds, 1)
        self._items: dict[str, float] = {}

    def seen(self, key: str) -> bool:
        now = time.monotonic()
        self._purge(now)

        expires_at = self._items.get(key)
        if expires_at and expires_at > now:
            return True

        self._items[key] = now + self.ttl_seconds
        return False

    def _purge(self, now: float) -> None:
        expired = [key for key, expires_at in self._items.items() if expires_at <= now]
        for key in expired:
            del self._items[key]

