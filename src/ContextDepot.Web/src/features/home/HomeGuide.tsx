import {
  FileTextIcon,
  FolderIcon,
  MessageSquareTextIcon,
  PlugIcon,
  RotateCcwIcon,
  ChevronDownIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { HomeGuideLayout } from "./HomeLayout";

const tips = [
  [FolderIcon, "homeTipTopic", "homeTipTopicHint"],
  [MessageSquareTextIcon, "homeTipContext", "homeTipContextHint"],
  [FileTextIcon, "homeTipDocument", "homeTipDocumentHint"],
  [RotateCcwIcon, "homeTipRecall", "homeTipRecallHint"],
] as const;

export function HomeGuide() {
  const { t } = useTranslation();
  return (
    <HomeGuideLayout title={t("homeTipsTitle")}>
      <ol className="home-guide-tips">
        {tips.map(([Icon, title, description]) => (
          <li className="home-about-row" key={title}>
            <Icon aria-hidden="true" />
            <div>
              <h3>{t(title)}</h3>
              <p>{t(description)}</p>
            </div>
          </li>
        ))}
      </ol>
      <details className="home-mcp-guide">
        <summary>
          <PlugIcon aria-hidden="true" />
          {t("homeMcpTitle")}
          <ChevronDownIcon className="home-guide-chevron" aria-hidden="true" />
        </summary>
        <div className="home-mcp-content">
          <p>{t("homeMcpHint")}</p>
          <dl>
            <div>
              <dt>{t("homeMcpEndpointLabel")}</dt>
              <dd>
                <code>{t("homeMcpEndpointValue")}</code>
              </dd>
            </div>
            <div>
              <dt>{t("homeMcpHeaderLabel")}</dt>
              <dd>
                <code>X-ContextDepot-Key</code>
                <span>{t("homeMcpKeyValue")}</span>
              </dd>
            </div>
          </dl>
          <p className="home-mcp-access">{t("homeMcpAccessHint")}</p>
        </div>
      </details>
    </HomeGuideLayout>
  );
}
