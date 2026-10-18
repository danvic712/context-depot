import "@/styles/search-box.css";
import { useRef, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { ArrowRightIcon, SearchIcon, XIcon } from "lucide-react";
import { SearchShortcut } from "@/components/content/SearchShortcut";
import { Button } from "@/components/ui/button";
import { Field, FieldGroup, FieldLabel } from "../../components/ui/field";
import {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupInput,
} from "../../components/ui/input-group";

export function SearchBox({
  value,
  onSearch,
  home = false,
  sidebar = false,
  onChange,
}: {
  home?: boolean;
  sidebar?: boolean;
  value: string;
  onSearch: (q: string) => void;
  onChange?: (q: string) => void;
}) {
  const [{ source, text: input }, setDraft] = useState({
    source: value,
    text: value,
  });
  const composing = useRef(false);
  const inputRef = useRef<HTMLInputElement>(null);
  // Keep the browser's composition text local until the IME commits it.
  // External URL changes (including Back/Forward) still restore the input.
  if (source !== value) setDraft({ source: value, text: value });
  function change(text: string, publish = true) {
    setDraft({ source: value, text });
    if (publish) onChange?.(text);
  }
  const { t } = useTranslation();
  function submit(e: FormEvent) {
    e.preventDefault();
    if (composing.current) return;
    if (home && !input.trim()) {
      inputRef.current?.focus();
      return;
    }
    onSearch(input.trim());
  }
  return (
    <form
      className="search-box"
      data-sidebar={sidebar || undefined}
      role="search"
      onSubmit={submit}
    >
      <FieldGroup>
        <Field>
          <FieldLabel htmlFor="knowledge-search">
            {t(sidebar ? "searchQueryLabel" : "dialogSearchTitle")}
          </FieldLabel>
          <InputGroup className="search-input-group">
            <InputGroupInput
              id="knowledge-search"
              ref={inputRef}
              value={input}
              onChange={(e) => change(e.target.value, !composing.current)}
              onCompositionStart={() => {
                composing.current = true;
              }}
              onCompositionEnd={(e) => {
                composing.current = false;
                change(e.currentTarget.value);
              }}
              onKeyDown={(e) => {
                // Safari can finish composition before the confirming keydown.
                if (
                  composing.current ||
                  e.nativeEvent.isComposing ||
                  e.keyCode === 229
                ) {
                  e.stopPropagation();
                  if (e.key === "Enter") e.preventDefault();
                }
              }}
              autoComplete="off"
              spellCheck={false}
              enterKeyHint="search"
              maxLength={4000}
              placeholder={t("searchPlaceholder")}
            />
            <InputGroupAddon align="inline-start">
              <SearchIcon aria-hidden="true" />
            </InputGroupAddon>
            <InputGroupAddon align="inline-end">
              {home && <SearchShortcut className="home-search-shortcut" />}
              {input && (
                <InputGroupButton
                  type="button"
                  variant="outline"
                  size="icon-sm"
                  aria-label={t(sidebar ? "searchClearQuery" : "clear")}
                  title={t(sidebar ? "searchClearQuery" : "clear")}
                  onClick={() => {
                    change("");
                    if (!home && !onChange) onSearch("");
                    inputRef.current?.focus();
                  }}
                >
                  <XIcon aria-hidden="true" />
                </InputGroupButton>
              )}
              {!sidebar && (
                <InputGroupButton type="submit" variant="default" size="sm">
                  {t("searchButton")}
                  <ArrowRightIcon data-icon="inline-end" aria-hidden="true" />
                </InputGroupButton>
              )}
            </InputGroupAddon>
          </InputGroup>
          {sidebar && (
            <Button type="submit" size="sm" className="search-sidebar-submit">
              {t("searchButton")}
              <ArrowRightIcon data-icon="inline-end" aria-hidden="true" />
            </Button>
          )}
        </Field>
      </FieldGroup>
    </form>
  );
}
