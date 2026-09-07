const assert = require("node:assert/strict");
const { afterEach, beforeEach, test } = require("node:test");
const { ApiError, createFarmTask } = require("../.test-build/lib/api.js");
const { saveSession } = require("../.test-build/lib/auth.js");

const session = {
  access_token: "test-access-token",
  refresh_token: "test-refresh-token",
  token_type: "bearer",
  expires_in: 3600,
  user: {
    id: "expert-1",
    phone_number: "",
    full_name: "Test Expert",
    province: null,
    district: null,
    role: "AGRONOMIST",
    profile_complete: true,
  },
};

const taskInput = {
  title: "Sulama",
  description: "Aksam sulama yap",
  reason: "Toprak kuru",
  priority: "HIGH",
  confidence: "MEDIUM",
};

let originalStorage;
beforeEach(() => {
  originalStorage = Object.getOwnPropertyDescriptor(globalThis, "localStorage");
  const values = new Map();
  Object.defineProperty(globalThis, "localStorage", {
    configurable: true,
    value: {
      getItem: (key) => values.get(key) ?? null,
      setItem: (key, value) => values.set(key, value),
      removeItem: (key) => values.delete(key),
    },
  });
  saveSession(session);
});

afterEach(() => {
  if (originalStorage) {
    Object.defineProperty(globalThis, "localStorage", originalStorage);
  } else {
    delete globalThis.localStorage;
  }
});

function taskResponse(dueDate) {
  return {
    id: "task-1",
    farm_id: "farm-1",
    ...taskInput,
    due_date: dueDate,
    status: "NEW",
    source: "EXPERT",
  };
}

for (const selectedDate of ["2030-07-20", "2028-02-29", "2030-01-01"]) {
  test(`createFarmTask sends ${selectedDate} as due_date without changing the day`, async (t) => {
    const requests = [];
    t.mock.method(globalThis, "fetch", async (url, init) => {
      requests.push({ url, init });
      return Response.json(taskResponse(selectedDate), { status: 201 });
    });

    const result = await createFarmTask("farm-1", {
      ...taskInput,
      dueDate: selectedDate,
    });

    assert.equal(requests.length, 1);
    assert.equal(new URL(requests[0].url).pathname, "/api/v1/farms/farm-1/tasks");
    assert.equal(requests[0].init.method, "POST");
    assert.equal(requests[0].init.headers.Authorization, "Bearer test-access-token");
    const body = JSON.parse(requests[0].init.body);
    assert.deepEqual(body, { ...taskInput, due_date: selectedDate });
    assert.equal(Object.hasOwn(body, "dueDate"), false);
    assert.equal(result.due_date, selectedDate);
  });
}

test("createFarmTask preserves the selected date after refreshing an expired session", async (t) => {
  const requests = [];
  t.mock.method(globalThis, "fetch", async (url, init) => {
    requests.push({ url, init });
    if (requests.length === 1) {
      return Response.json({ detail: "expired" }, { status: 401 });
    }
    if (new URL(url).pathname === "/api/v1/auth/refresh") {
      return Response.json({ ...session, access_token: "refreshed-token" });
    }
    return Response.json(taskResponse("2030-07-20"), { status: 201 });
  });

  await createFarmTask("farm-1", { ...taskInput, dueDate: "2030-07-20" });

  assert.equal(requests.length, 3);
  assert.equal(new URL(requests[1].url).pathname, "/api/v1/auth/refresh");
  assert.equal(requests[2].init.headers.Authorization, "Bearer refreshed-token");
  assert.equal(requests[0].init.body, requests[2].init.body);
  assert.equal(JSON.parse(requests[2].init.body).due_date, "2030-07-20");
});

test("createFarmTask surfaces an invalid-date response instead of reporting success", async (t) => {
  t.mock.method(globalThis, "fetch", async () =>
    Response.json({ detail: "Invalid date" }, { status: 400 }),
  );

  await assert.rejects(
    createFarmTask("farm-1", { ...taskInput, dueDate: "invalid" }),
    (error) => error instanceof ApiError && error.status === 400,
  );
});
