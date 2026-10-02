import { useEffect, useRef, useState } from "react";
import { useLocation, useSearchParams } from "react-router";
import { hitKey, type ContextKind, type SearchHit } from "./search-api";
import {
  useKnowledgeSearch,
  useKnowledgePreview,
  useSearchWorkspaces,
} from "./use-knowledge-search";

export function useSearchPage() {
  const [params, setParams] = useSearchParams();
  const location = useLocation();
  const query = params.get("q") ?? "";
  const workspace = params.get("workspace") ?? undefined;
  const kind = params.get("kind") as ContextKind | undefined;
  const [retry, setRetry] = useState(0);
  const [spaceRetry, setSpaceRetry] = useState(0);
  const [previewRetry, setPreviewRetry] = useState(0);
  const [immediate, setImmediate] = useState(false);
  const spaces = useSearchWorkspaces(spaceRetry);
  const resource = useKnowledgeSearch(
    query,
    workspace,
    retry,
    kind ?? undefined,
    immediate,
  );
  const hits = (resource.data?.items ?? []).filter(
    (hit) =>
      !params.has("type") ||
      hit.type === (params.get("type") === "contexts" ? "context" : "document"),
  );
  const selected =
    hits.find((hit) => hitKey(hit) === params.get("selected")) ?? hits[0];
  const detail = useKnowledgePreview(selected, previewRetry);
  const reading = params.get("read") === "1" && !!selected;
  const listRef = useRef<HTMLDivElement>(null);
  const paneRef = useRef<HTMLElement>(null);
  const selectedKey = selected ? hitKey(selected) : "";
  const readingBefore = useRef(reading);
  const scrollBefore = useRef({ list: 0, page: 0 });
  useEffect(() => {
    if (readingBefore.current && !reading) {
      requestAnimationFrame(() => {
        if (listRef.current)
          listRef.current.scrollTop = scrollBefore.current.list;
        window.scrollTo({ top: scrollBefore.current.page });
        listRef.current
          ?.querySelector<HTMLButtonElement>("[data-selected]")
          ?.focus({ preventScroll: true });
      });
    }
    readingBefore.current = reading;
  }, [reading]);
  useEffect(() => {
    if (reading && selectedKey) {
      if (window.matchMedia("(max-width: 1100px)").matches)
        window.scrollTo({ top: 0 });
      paneRef.current?.focus({ preventScroll: true });
    }
  }, [reading, selectedKey]);

  function update(next: URLSearchParams, replace = false) {
    setParams(next, {
      replace,
      state: location.state,
      preventScrollReset: true,
    });
  }
  function changeQuery(value: string, submit = false) {
    const next = new URLSearchParams(params);
    if (value) next.set("q", value);
    else next.delete("q");
    next.delete("selected");
    next.delete("read");
    setImmediate(submit);
    if (submit) setRetry((value) => value + 1);
    update(next, !submit);
  }
  function filter(key: "type" | "kind" | "workspace", value: string) {
    const next = new URLSearchParams(params);
    if (value === "all") next.delete(key);
    else next.set(key, value);
    if (key === "type" && value !== "contexts") next.delete("kind");
    if (key === "kind" && value !== "all") next.set("type", "contexts");
    next.delete("selected");
    next.delete("read");
    setImmediate(true);
    update(next);
  }
  function reset() {
    const next = new URLSearchParams(params);
    for (const key of ["type", "kind", "workspace", "selected", "read"])
      next.delete(key);
    setImmediate(true);
    setRetry((value) => value + 1);
    update(next);
  }
  function select(hit: SearchHit, open: boolean) {
    if (open && !reading)
      scrollBefore.current = {
        list: listRef.current?.scrollTop ?? 0,
        page: window.scrollY,
      };
    const next = new URLSearchParams(params);
    next.set("selected", hitKey(hit));
    if (open) next.set("read", "1");
    else next.delete("read");
    update(next, !open || reading);
  }
  function back() {
    const next = new URLSearchParams(params);
    next.delete("read");
    update(next, true);
  }
  return {
    params,
    query,
    resource,
    hits,
    selected,
    detail,
    spaces,
    reading,
    listRef,
    paneRef,
    degraded: resource.data?.degraded,
    changeQuery,
    filter,
    reset,
    select,
    back,
    retrySearch: () => setRetry((value) => value + 1),
    retrySpaces: () => setSpaceRetry((value) => value + 1),
    retryPreview: () => setPreviewRetry((value) => value + 1),
  };
}
