export enum WorkspaceContentStatus {
  All = "all",
  Populated = "populated",
  Empty = "empty",
}
export enum WorkspaceContentType {
  All = "all",
  Contexts = "contexts",
  Documents = "documents",
  Both = "both",
}
export enum WorkspaceUpdateWindow {
  All = "all",
  Week = "7",
  Month = "30",
  Quarter = "90",
  Custom = "custom",
}
export enum WorkspaceSortOrder {
  Activity = "activity",
  Name = "name",
  Knowledge = "knowledge",
}
export enum DirectoryParameter {
  Keyword = "q",
  Status = "contentStatus",
  Content = "contents",
  Updated = "updated",
  From = "from",
  To = "to",
  Sort = "sort",
  Page = "page",
  PageSize = "pageSize",
  ReturnPage = "directoryPage",
  ReturnQuery = "directoryQuery",
  LegacyLayout = "layout",
}
export const directoryScrollStorageKey = "contextdepot.space-directory-scroll";
