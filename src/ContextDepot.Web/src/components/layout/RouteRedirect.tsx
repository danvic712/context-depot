import { Navigate } from "react-router";
import { useAppContext } from "@/hooks/use-app-context";

export function RouteRedirect() {
  const { linkTo } = useAppContext();
  return <Navigate to={linkTo("/")} replace />;
}
