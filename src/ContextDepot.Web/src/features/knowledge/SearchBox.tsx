import "@/styles/search-box.css";
import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { ArrowRightIcon, SearchIcon } from "lucide-react";
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
}: {
  value: string;
  onSearch: (q: string) => void;
}) {
  const [input, setInput] = useState(value);
  const { t } = useTranslation();
  function submit(e: FormEvent) {
    e.preventDefault();
    onSearch(input.trim());
  }
  return (
    <form className="search-box" role="search" onSubmit={submit}>
      <FieldGroup>
        <Field>
          <FieldLabel htmlFor="knowledge-search">
            {t("searchPlaceholder")}
          </FieldLabel>
          <InputGroup className="search-input-group">
            <InputGroupInput
              id="knowledge-search"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder={t("searchPlaceholder")}
            />
            <InputGroupAddon align="inline-start">
              <SearchIcon />
            </InputGroupAddon>
            <InputGroupAddon align="inline-end">
              {input && (
                <InputGroupButton
                  type="button"
                  onClick={() => {
                    setInput("");
                    onSearch("");
                  }}
                >
                  {t("clear")}
                </InputGroupButton>
              )}
              <InputGroupButton type="submit" variant="default" size="sm">
                {t("searchButton")}
                <ArrowRightIcon data-icon="inline-end" />
              </InputGroupButton>
            </InputGroupAddon>
          </InputGroup>
        </Field>
      </FieldGroup>
    </form>
  );
}
