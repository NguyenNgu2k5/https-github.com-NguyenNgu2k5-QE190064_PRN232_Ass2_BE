import assert from "node:assert/strict";
import { before, after, test } from "node:test";
import { createHmac, randomUUID } from "node:crypto";
import { spawnSync } from "node:child_process";

const base = process.env.API_BASE_URL ?? "http://127.0.0.1:5102/api";
const database = process.env.TEST_DATABASE_URL;
assert.ok(["localhost", "127.0.0.1"].includes(new URL(base).hostname), "Use an isolated local test API.");
assert.ok(database, "TEST_DATABASE_URL is required.");
const testDatabase = new URL(database);
assert.ok(["localhost", "127.0.0.1"].includes(testDatabase.hostname) && testDatabase.pathname.endsWith("_ass2"), "Use an isolated local AS2 test database.");
const prefix = "AS2Test" + randomUUID().replaceAll("-", "").slice(0, 8);
const ids = { accounts: [], tasks: [], projects: [], departments: [], tags: [] };
let admin, staff, department, project, tag1, tag2, task, owner;
function sql(query) {
  assert.ok(database, "TEST_DATABASE_URL must point to an isolated PostgreSQL test database.");
  const parsed = new URL(database);
  assert.ok(["localhost", "127.0.0.1"].includes(parsed.hostname) && parsed.pathname.endsWith("_ass2"), "Refusing to mutate a non-local/non-AS2 test database.");
  const result = spawnSync(process.env.PSQL_EXE ?? "psql", [database, "-X", "-A", "-t", "-v", "ON_ERROR_STOP=1", "-c", query], { encoding: "utf8" });
  assert.equal(result.status, 0, result.stderr);
  return result.stdout.trim();
}
async function request(path, method = "GET", body, token, status = 200) {
  const response = await fetch(base + path, { method, headers: { ...(body === undefined ? {} : { "Content-Type": "application/json" }), ...(token ? { Authorization: "Bearer " + token } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await response.text();
  assert.equal(response.status, status, method + " " + path + ": " + (response.status >= 400 ? text : "unexpected status"));
  return text ? JSON.parse(text) : undefined;
}
async function register(email, password, fullName = prefix) {
  const account = await request("/auth/register", "POST", { fullName, email, password, role: 1 }, undefined, 201);
  ids.accounts.push(account.accountId);
  assert.equal(account.role, 0);
  assert.ok(!("passwordHash" in account));
  return account;
}
const login = (email, password) => request("/auth/login", "POST", { email, password });
before(async () => {
  admin = await login(process.env.ADMIN_EMAIL, process.env.ADMIN_PASSWORD);
  assert.equal(admin.account.role, 1);
  try { staff = await login(process.env.STAFF_EMAIL, process.env.STAFF_PASSWORD); }
  catch { await request("/auth/register", "POST", { fullName: "Local Staff", email: process.env.STAFF_EMAIL, password: process.env.STAFF_PASSWORD }, undefined, 201); staff = await login(process.env.STAFF_EMAIL, process.env.STAFF_PASSWORD); }
});
after(() => {
  sql('DROP TRIGGER IF EXISTS "AS2TestFail" ON "TaskTag"; DROP FUNCTION IF EXISTS "AS2TestFail"();');
  const statement = (table, column, values) => values.length ? 'DELETE FROM "' + table + '" WHERE "' + column + '" IN (' + values.join(",") + ');' : "";
  sql("BEGIN;" + statement("TaskTag", "TaskID", ids.tasks) + statement("Task", "TaskID", ids.tasks) + statement("Project", "ProjectID", ids.projects) + statement("Department", "DepartmentID", ids.departments) + statement("Tag", "TagID", ids.tags) + statement("SystemAccount", "AccountID", ids.accounts) + "COMMIT;");
});
test("all AS1 GET routes remain public and department project counts are real", async () => {
  for (const path of ["/departments", "/departments/1", "/departments/search?name=Engineering", "/projects", "/projects/1", "/projects/department/1", "/projects/search?status=1", "/tasks", "/tasks/1", "/tasks/project/1", "/tasks/search?priority=0", "/tags"]) await request(path);
  assert.ok((await request("/departments")).find(d => d.departmentId === 1).projects.length > 0);
  await request("/tasks/2147483647", "GET", undefined, undefined, 404);
});
test("all twelve CRUD write routes require authentication; accounts distinguish 401 from 403", async () => {
  for (const kind of ["departments", "projects", "tasks", "tags"]) {
    for (const method of ["POST", "PUT", "DELETE"]) await request("/" + kind + (method === "POST" ? "" : "/2147483647"), method, method === "DELETE" ? undefined : {}, undefined, 401);
  }
  for (const [path, method, body] of [["/accounts", "GET"], ["/accounts/1", "GET"], ["/accounts/1", "PUT", { fullName: "Blocked", role: 1 }], ["/accounts/1", "DELETE"]]) {
    await request(path, method, body, undefined, 401);
    await request(path, method, body, staff.token, 403);
  }
});
test("register normalizes unique emails, ignores role overposting, hashes passwords and handles concurrent duplicates", async () => {
  const email = prefix.toLowerCase() + "@example.com", password = "TestPassword123!";
  const account = await register(email.toUpperCase(), password);
  assert.equal(account.email, email);
  await request("/auth/register", "POST", { fullName: prefix, email, password }, undefined, 409);
  const hash = sql('SELECT "PasswordHash" FROM "SystemAccount" WHERE "AccountID"=' + account.accountId);
  assert.notEqual(hash, password);
  assert.ok(Buffer.from(hash, "base64").length > 32);
  for (const payload of [{ fullName: "", email, password }, { fullName: " ", email: prefix + "blank@example.com", password }, { fullName: prefix, email: "bad", password }, { fullName: prefix, email, password: "short" }]) await request("/auth/register", "POST", payload, undefined, 400);
  const concurrent = prefix + "race@example.com";
  const responses = await Promise.all([1, 2].map(() => fetch(base + "/auth/register", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ fullName: prefix, email: concurrent, password }) })));
  assert.deepEqual(responses.map(r => r.status).sort(), [201, 409]);
  for (const response of responses) if (response.status === 201) ids.accounts.push((await response.json()).accountId);
});
test("login rejects invalid credentials, validates signature/expiry and returns mandatory JWT claims", async () => {
  await request("/auth/login", "POST", { email: process.env.STAFF_EMAIL, password: "wrongpassword" }, undefined, 401);
  await request("/auth/login", "POST", { email: prefix + "absent@example.com", password: "wrongpassword" }, undefined, 401);
  const claims = JSON.parse(Buffer.from(staff.token.split(".")[1], "base64url"));
  assert.equal(Number(claims.AccountID), staff.account.accountId);
  assert.equal(claims.Email, staff.account.email);
  assert.equal(claims.Role, "Staff");
  assert.ok(claims.exp * 1000 > Date.now() && claims.exp * 1000 <= Date.now() + 86401000);
  assert.ok(!("passwordHash" in staff.account));
  await request("/auth/me", "GET", undefined, staff.token);
  const parts = staff.token.split(".");
  await request("/auth/me", "GET", undefined, parts.slice(0, 2).join(".") + ".invalid-signature", 401);
  assert.ok(process.env.JWT_SECRET, "JWT_SECRET required for a correctly signed expired token test.");
  const expired = { ...claims, exp: Math.floor(Date.now() / 1000) - 1, nbf: Math.floor(Date.now() / 1000) - 100 };
  const headerAndBody = parts[0] + "." + Buffer.from(JSON.stringify(expired)).toString("base64url");
  const token = headerAndBody + "." + createHmac("sha256", process.env.JWT_SECRET).update(headerAndBody).digest("base64url");
  await request("/auth/me", "GET", undefined, token, 401);
  for (const kind of ["departments", "projects", "tasks", "tags"]) {
    for (const method of ["POST", "PUT", "DELETE"]) {
      const path = "/" + kind + (method === "POST" ? "" : "/2147483647");
      const payload = method === "DELETE" ? undefined : {};
      await request(path, method, payload, token, 401);
      await request(path, method, payload, "invalid.token.signature", 401);
    }
  }
});
test("Staff can create and update all four resources; Low priority persists and task ownership comes from JWT", async () => {
  department = await request("/departments", "POST", { departmentName: prefix, departmentDescription: "Integration fixture" }, staff.token);
  ids.departments.push(department.departmentId);
  await request("/departments/" + department.departmentId, "PUT", { departmentName: prefix + " updated", departmentDescription: "Updated" }, staff.token);
  project = await request("/projects", "POST", { projectName: prefix, startDate: "2026-10-10", endDate: "2026-11-10", status: 0, departmentId: department.departmentId }, staff.token);
  ids.projects.push(project.projectId);
  await request("/projects/" + project.projectId, "PUT", { projectName: prefix + " updated", startDate: "2026-10-10", status: 1, departmentId: department.departmentId }, staff.token);
  tag1 = await request("/tags", "POST", { tagName: prefix + "A", color: "#2563EB" }, staff.token);
  tag2 = await request("/tags", "POST", { tagName: prefix + "B", color: "#16A34A" }, staff.token);
  ids.tags.push(tag1.tagId, tag2.tagId);
  await request("/tags/" + tag1.tagId, "PUT", { tagName: prefix + "A updated", color: "#2563EB" }, staff.token);
  const creator = await register(prefix + "owner@example.com", "TestPassword123!");
  owner = await login(creator.email, "TestPassword123!");
  task = await request("/tasks", "POST", { title: prefix, projectId: project.projectId, priority: 0, tagIds: [tag1.tagId, tag1.tagId], createdById: admin.account.accountId }, owner.token);
  ids.tasks.push(task.taskId);
  assert.equal(task.priority, 0);
  assert.equal(task.tags.length, 1);
  assert.equal(Number(sql('SELECT "CreatedByID" FROM "Task" WHERE "TaskID"=' + task.taskId)), creator.accountId);
  assert.ok((await request("/departments/search?name=" + prefix))[0].projects.some(p => p.projectId === project.projectId));
});
test("task tag updates retain, add, remove and clear links; invalid tags leave the task unchanged", async () => {
  const update = tagIds => request("/tasks/" + task.taskId, "PUT", { title: prefix, projectId: project.projectId, priority: 0, tagIds }, staff.token);
  const both = await update([tag1.tagId, tag2.tagId]);
  assert.deepEqual(both.tags.map(t => t.tagId).sort(), [tag1.tagId, tag2.tagId].sort());
  assert.equal((await update([tag2.tagId])).tags[0].tagId, tag2.tagId);
  assert.equal((await update([])).tags.length, 0);
  await update([tag1.tagId]);
  await request("/tasks/" + task.taskId, "PUT", { title: "Should never persist", projectId: project.projectId, priority: 3, tagIds: [2147483647] }, staff.token, 400);
  const unchanged = await request("/tasks/" + task.taskId);
  assert.equal(unchanged.title, prefix);
  assert.equal(unchanged.priority, 0);
  assert.equal(unchanged.tags[0].tagId, tag1.tagId);
});
test("a database failure rolls back both task fields and tag links", async () => {
  sql('CREATE FUNCTION "AS2TestFail"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION \'injected test failure\' USING ERRCODE=\'23503\'; END; $$; CREATE TRIGGER "AS2TestFail" BEFORE INSERT ON "TaskTag" FOR EACH ROW EXECUTE FUNCTION "AS2TestFail"();');
  try {
    await request("/tasks/" + task.taskId, "PUT", { title: "Should roll back", projectId: project.projectId, priority: 3, tagIds: [tag2.tagId] }, staff.token, 409);
    const unchanged = await request("/tasks/" + task.taskId);
    assert.equal(unchanged.title, prefix);
    assert.equal(unchanged.priority, 0);
    assert.deepEqual(unchanged.tags.map(t => t.tagId), [tag1.tagId]);
  } finally { sql('DROP TRIGGER "AS2TestFail" ON "TaskTag"; DROP FUNCTION "AS2TestFail"();'); }
});
test("Admin account updates accept only name/role; role changes and deletion invalidate old tokens", async () => {
  const account = await register(prefix + "role@example.com", "TestPassword123!");
  const old = await login(account.email, "TestPassword123!");
  const promoted = await request("/accounts/" + account.accountId, "PUT", { role: 1, email: "ignored@example.com", passwordHash: "ignored" }, admin.token);
  assert.equal(promoted.fullName, account.fullName);
  assert.equal(promoted.email, account.email);
  await request("/auth/me", "GET", undefined, old.token, 401);
  const renamed = await request("/accounts/" + account.accountId, "PUT", { fullName: prefix + " renamed" }, admin.token);
  assert.equal(renamed.role, 1);
  await request("/accounts/" + account.accountId, "PUT", {}, admin.token, 400);
  await request("/accounts/" + account.accountId, "PUT", { role: 2 }, admin.token, 400);
  const current = await login(account.email, "TestPassword123!");
  await request("/accounts/" + account.accountId, "DELETE", undefined, admin.token, 204);
  await request("/auth/me", "GET", undefined, current.token, 401);
  await request("/accounts/" + account.accountId, "GET", undefined, admin.token, 404);
  for (const item of await request("/accounts", "GET", undefined, admin.token)) assert.ok(!("passwordHash" in item));
});
test("account deletion is blocked for active AND soft-deleted tasks; deleted tasks cannot be edited", async () => {
  await request("/accounts/" + owner.account.accountId, "DELETE", undefined, admin.token, 409);
  await request("/tasks/" + task.taskId, "DELETE", undefined, staff.token, 204);
  await request("/tasks/" + task.taskId, "GET", undefined, undefined, 404);
  assert.ok(!(await request("/tasks")).some(t => t.taskId === task.taskId));
  await request("/accounts/" + owner.account.accountId, "DELETE", undefined, admin.token, 409);
  await request("/tasks/" + task.taskId, "PUT", { title: prefix, projectId: project.projectId }, staff.token, 404);
  await request("/projects/" + project.projectId, "DELETE", undefined, staff.token, 400);
  await request("/departments/" + department.departmentId, "DELETE", undefined, staff.token, 400);
});
test("Staff deletion works for unlinked records and concurrent tag names cannot bypass database uniqueness", async () => {
  const tag = await request("/tags", "POST", { tagName: prefix + "remove", color: "#2563EB" }, staff.token);
  ids.tags.push(tag.tagId);
  await request("/tags/" + tag.tagId, "DELETE", undefined, staff.token, 204);
  const project2 = await request("/projects", "POST", { projectName: prefix + "remove", startDate: "2026-10-10", departmentId: department.departmentId }, staff.token);
  ids.projects.push(project2.projectId);
  await request("/projects/" + project2.projectId, "DELETE", undefined, staff.token, 204);
  const department2 = await request("/departments", "POST", { departmentName: prefix + "remove", departmentDescription: "Remove me" }, staff.token);
  ids.departments.push(department2.departmentId);
  await request("/departments/" + department2.departmentId, "DELETE", undefined, staff.token, 204);
  const name = prefix + "RaceTag";
  const results = await Promise.all([name, name.toLowerCase()].map(tagName => fetch(base + "/tags", { method: "POST", headers: { "Content-Type": "application/json", Authorization: "Bearer " + staff.token }, body: JSON.stringify({ tagName }) })));
  assert.equal(results.filter(r => r.status === 200).length, 1);
  assert.ok(results.some(r => [400, 409].includes(r.status)));
  for (const r of results) if (r.status === 200) ids.tags.push((await r.json()).tagId);
});

test("Admin can perform every business CRUD operation; editing a task does not change its creator", async () => {
  const d = await request("/departments", "POST", { departmentName: prefix + " Admin", departmentDescription: "Admin fixture" }, admin.token);
  ids.departments.push(d.departmentId);
  await request("/departments/" + d.departmentId, "PUT", { departmentName: prefix + " Admin", departmentDescription: "Edited" }, admin.token);
  const p = await request("/projects", "POST", { projectName: prefix + " Admin", startDate: "2026-10-10", departmentId: d.departmentId }, admin.token);
  ids.projects.push(p.projectId);
  await request("/projects/" + p.projectId, "PUT", { projectName: prefix + " Admin", startDate: "2026-10-10", departmentId: d.departmentId, status: 1 }, admin.token);
  const g = await request("/tags", "POST", { tagName: prefix + " Admin", color: "#2563EB" }, admin.token);
  ids.tags.push(g.tagId);
  await request("/tags/" + g.tagId, "PUT", { tagName: prefix + " Admin", color: "#16A34A" }, admin.token);
  const t = await request("/tasks", "POST", { title: prefix + " Admin", projectId: p.projectId, priority: 0, tagIds: [g.tagId] }, admin.token);
  ids.tasks.push(t.taskId);
  await request("/tasks/" + t.taskId, "PUT", { title: prefix + " Admin edited", projectId: p.projectId, priority: 0, tagIds: [] }, admin.token);
  await request("/tasks/" + t.taskId, "PUT", { title: prefix + " Staff edited", projectId: p.projectId, priority: 0, tagIds: [] }, staff.token);
  assert.equal(Number(sql('SELECT "CreatedByID" FROM "Task" WHERE "TaskID"=' + t.taskId)), admin.account.accountId);
  await request("/tasks/" + t.taskId, "DELETE", undefined, admin.token, 204);
  sql('DELETE FROM "TaskTag" WHERE "TaskID"=' + t.taskId + '; DELETE FROM "Task" WHERE "TaskID"=' + t.taskId);
  await request("/tags/" + g.tagId, "DELETE", undefined, admin.token, 204);
  await request("/projects/" + p.projectId, "DELETE", undefined, admin.token, 204);
  await request("/departments/" + d.departmentId, "DELETE", undefined, admin.token, 204);
});
