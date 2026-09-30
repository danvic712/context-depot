import { Link, useLocation } from "react-router";
import { ArrowRightIcon, FileTextIcon, SearchIcon } from "lucide-react";
import {
  Item,
  ItemActions,
  ItemContent,
  ItemDescription,
  ItemMedia,
  ItemTitle,
} from "@/components/ui/item";
import { useAppContext } from "@/hooks/use-app-context";
import { sampleHref, type SampleKnowledge } from "./sample-data";

export function SampleRow({ item }: { item: SampleKnowledge }) {
  const Icon = item.type === "document" ? FileTextIcon : SearchIcon;
  const { linkTo } = useAppContext();
  const location = useLocation();
  return (
    <div role="listitem">
      <Item asChild className="w-full rounded-none text-left">
        <Link
          to={linkTo(sampleHref(item))}
          state={{ from: location.pathname + location.search + location.hash }}
        >
          <ItemMedia variant="icon">
            <Icon aria-hidden="true" />
          </ItemMedia>
          <ItemContent className="min-w-0">
            <ItemTitle>{item.title}</ItemTitle>
            <ItemDescription>
              {item.workspace} · {item.kind}
            </ItemDescription>
            <ItemDescription>{item.summary}</ItemDescription>
          </ItemContent>
          <ItemActions>
            <ArrowRightIcon aria-hidden="true" />
          </ItemActions>
        </Link>
      </Item>
    </div>
  );
}
