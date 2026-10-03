import "@/styles/search-box.css";
import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { ArrowRightIcon, SearchIcon, XIcon } from "lucide-react";
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
  onChange,
}: {
  home?: boolean;
  value: string;
  onSearch: (q: string) => void;
  onChange?: (q: string) => void;
}) {
  const [draft, setDraft] = useState(value);
  const input = onChange ? value : draft;
  const change = (value: string) =>
    onChange ? onChange(value) : setDraft(value);
  const { t } = useTranslation();
  function submit(e: FormEvent) {
    e.preventDefault();
    if (home && !input.trim()) {
      document.getElementById("knowledge-search")?.focus();
      return;
    }
    onSearch(input.trim());
  }
  return (
    <form className="search-box" role="search" onSubmit={submit}>
      <FieldGroup>
        <Field>
          <FieldLabel htmlFor="knowledge-search">
            {t("dialogSearchTitle")}
          </FieldLabel>
          <InputGroup className="search-input-group">
            <InputGroupInput
              id="knowledge-search"
              value={input}
              onChange={(e) => change(e.target.value)}
              maxLength={4000}
              placeholder={t("searchPlaceholder")}
            />
            <InputGroupAddon align="inline-start">
              <SearchIcon aria-hidden="true" />
            </InputGroupAddon>
            <InputGroupAddon align="inline-end">
              {home && <kbd className="home-search-shortcut">⌘ / Ctrl K</kbd>}
              {input && (
                <InputGroupButton
                  type="button"
                  variant="outline"
                  size="icon-sm"
                  aria-label={t("clear")}
                  title={t("clear")}
                  onClick={() => {
                    change("");
                    if (!home && !onChange) onSearch("");
                    document.getElementById("knowledge-search")?.focus();
                  }}
                >
                  <XIcon aria-hidden="true" />
                </InputGroupButton>
              )}
              <InputGroupButton type="submit" variant="default" size="sm">
                {t("searchButton")}
                <ArrowRightIcon data-icon="inline-end" aria-hidden="true" />
              </InputGroupButton>
            </InputGroupAddon>
          </InputGroup>
        </Field>
      </FieldGroup>
    </form>
  );
}
