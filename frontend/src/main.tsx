import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createBrowserRouter, RouterProvider } from "react-router";
import "./index.css";
import { AuthProvider } from "./auth/AuthProvider";
import { ApiError } from "./lib/api";
import { Layout } from "./components/Layout";
import { HomePage } from "./pages/HomePage";
import { MoviePage } from "./pages/MoviePage";
import { SearchPage } from "./pages/SearchPage";
import { LoginPage, RegisterPage } from "./pages/AuthPages";
import { NotFoundPage } from "./pages/NotFoundPage";
import { ProfilePage, UserPage } from "./pages/ProfilePage";
import { RequireAuth } from "./auth/RequireAuth";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000,
      // Client errors (404, 400) will not change on retry.
      retry: (failureCount, error) =>
        !(error instanceof ApiError && error.status < 500) && failureCount < 2,
    },
  },
});

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: "movie/:tmdbId", element: <MoviePage /> },
      { path: "search", element: <SearchPage /> },
      { path: "user/:userId", element: <UserPage /> },
      { path: "login", element: <LoginPage /> },
      { path: "register", element: <RegisterPage /> },
      {
        element: <RequireAuth />,
        children: [{ path: "profile", element: <ProfilePage /> }],
      },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>
  </StrictMode>,
);
