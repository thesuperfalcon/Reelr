import { Navigate, Outlet, useLocation } from "react-router";
import { useAuth } from "./auth-context";

// Sends visitors to the login page. LoginPage returns them here after login.
export function RequireAuth() {
  const { user } = useAuth();
  const location = useLocation();

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }

  return <Outlet />;
}
