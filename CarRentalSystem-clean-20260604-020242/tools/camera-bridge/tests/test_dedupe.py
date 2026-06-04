import time

from camera_bridge.dedupe import DedupeCache


def test_dedupe_blocks_key_until_ttl_expires():
    cache = DedupeCache(ttl_seconds=1)

    assert cache.seen("cam:plate") is False
    assert cache.seen("cam:plate") is True

    time.sleep(1.05)

    assert cache.seen("cam:plate") is False

