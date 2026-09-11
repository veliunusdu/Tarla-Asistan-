const assert = require("node:assert/strict");
const { test } = require("node:test");

const {
  LatestRequestGuard,
} = require("../.test-build/lib/latest-request-guard.js");

test("a newer request makes an older response stale", () => {
  const guard = new LatestRequestGuard();

  const olderRequest = guard.begin();
  const latestRequest = guard.begin();

  assert.equal(guard.isCurrent(olderRequest), false);
  assert.equal(guard.isCurrent(latestRequest), true);
});

test("invalidating the guard prevents an in-flight response from updating state", () => {
  const guard = new LatestRequestGuard();
  const request = guard.begin();

  guard.invalidate(request);

  assert.equal(guard.isCurrent(request), false);
});
