import { afterAll, afterEach, describe, expect, spyOn, test } from "bun:test";
import { toast } from "sonner";
import { httpRequest, isRequestCanceled } from "../../src/lib/http-client";

const server = Bun.serve({
  port: 0,
  fetch(request) {
    if (new URL(request.url).pathname === "/failure")
      return Response.json({ code: "InternalError" }, { status: 500 });
    return Response.json({ saved: true });
  },
});
afterAll(() => server.stop(true));

const success = spyOn(toast, "success");
const failure = spyOn(toast, "error");
afterEach(() => {
  success.mockClear();
  failure.mockClear();
});
afterAll(() => {
  success.mockRestore();
  failure.mockRestore();
});

const options = {
  baseURL: server.url.toString(),
  url: "/success",
  parse: (data: unknown) => data,
  feedback: { success: () => "Saved", error: "Failed" },
};

describe("Shared axios request feedback", () => {
  test("repeated saves update one toast instead of stacking messages", async () => {
    const id = "language-save-test";
    await httpRequest({
      ...options,
      feedback: { id, success: "Saved in English" },
    });
    await httpRequest({ ...options, feedback: { id, success: "已保存" } });
    let active = toast.getToasts().filter((item) => item.id === id);
    expect(active).toHaveLength(1);
    expect(active[0].title).toBe("已保存");
    await expect(
      httpRequest({
        ...options,
        url: "/failure",
        feedback: { id, error: "Failed" },
      }),
    ).rejects.toThrow();
    active = toast.getToasts().filter((item) => item.id === id);
    expect(active).toHaveLength(1);
    expect(active[0].type).toBe("error");
    expect(active[0].title).toBe("Failed");
  });
  test("shows one success after parsing the response", async () => {
    expect(await httpRequest(options)).toEqual({ saved: true });
    expect(success).toHaveBeenCalledTimes(1);
    expect(success).toHaveBeenCalledWith("Saved", undefined);
    expect(failure).not.toHaveBeenCalled();
  });

  test("shows one failure and keeps the HTTP rejection", async () => {
    await expect(
      httpRequest({ ...options, url: "/failure" }),
    ).rejects.toThrow();
    expect(failure).toHaveBeenCalledTimes(1);
    expect(failure).toHaveBeenCalledWith("Failed", undefined);
    expect(success).not.toHaveBeenCalled();
  });

  test("an invalid response reports failure instead of success", async () => {
    await expect(
      httpRequest({
        ...options,
        parse: () => {
          throw new Error("Invalid response");
        },
      }),
    ).rejects.toThrow("Invalid response");
    expect(failure).toHaveBeenCalledTimes(1);
    expect(failure).toHaveBeenCalledWith("Failed", undefined);
    expect(success).not.toHaveBeenCalled();
  });

  test("preparation failures also use the shared error feedback", async () => {
    await expect(
      httpRequest({
        ...options,
        beforeRequest: async () => {
          throw new Error("Resource load failed");
        },
      }),
    ).rejects.toThrow("Resource load failed");
    expect(failure).toHaveBeenCalledTimes(1);
    expect(failure).toHaveBeenCalledWith("Failed", undefined);
    expect(success).not.toHaveBeenCalled();
  });

  test("canceled requests stay silent", async () => {
    const controller = new AbortController();
    controller.abort();
    try {
      await httpRequest({ ...options, signal: controller.signal });
      throw new Error("Expected cancellation");
    } catch (error) {
      expect(isRequestCanceled(error)).toBe(true);
    }
    expect(failure).not.toHaveBeenCalled();
    expect(success).not.toHaveBeenCalled();
  });

  test("background requests without feedback stay silent", async () => {
    await httpRequest({ ...options, feedback: undefined });
    await expect(
      httpRequest({ ...options, url: "/failure", feedback: undefined }),
    ).rejects.toThrow();
    expect(failure).not.toHaveBeenCalled();
    expect(success).not.toHaveBeenCalled();
  });
});
