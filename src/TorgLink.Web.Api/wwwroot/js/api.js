export async function api(path, init) {
  const res = await fetch(path, {
    credentials: "include",
    ...init,
    headers: {
      ...(init && init.body instanceof FormData ? {} : { "Content-Type": "application/json" }),
      ...((init && init.headers) || {})
    }
  });
  if (res.status === 401) {
    throw Object.assign(new Error("unauthorized"), { status: 401 });
  }
  if (!res.ok) {
    let msg = res.statusText;
    try {
      const j = await res.json();
      msg = j.error ?? j.title ?? msg;
    } catch { /* ignore */ }
    throw Object.assign(new Error(msg), { status: res.status });
  }
  if (res.status === 204) return undefined;
  const ct = res.headers.get("content-type") ?? "";
  if (ct.includes("application/json")) return await res.json();
  return undefined;
}

export const get = (path) => api(path);
export const post = (path, body) =>
  api(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });
export const del = (path) => api(path, { method: "DELETE" });

export async function postFile(path, file, field = "file") {
  const fd = new FormData();
  fd.append(field, file);
  const res = await fetch(path, { method: "POST", body: fd, credentials: "include" });
  if (!res.ok) {
    let msg = res.statusText;
    try {
      const j = await res.json();
      msg = j.error ?? msg;
    } catch { /* ignore */ }
    throw new Error(msg);
  }
  return await res.json();
}
