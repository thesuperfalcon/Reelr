import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createBrowserRouter, RouterProvider } from "react-router";
// Fonts are served from this site, so visitors' browsers do not contact Google.
import "@fontsource/big-shoulders-display/600";
import "@fontsource/big-shoulders-display/800";
import "@fontsource/instrument-sans/400";
import "@fontsource/instrument-sans/500";
import "@fontsource/instrument-sans/600";
import "./index.css";
import { AuthProvider } from "./auth/AuthProvider";
import { ApiError } from "./lib/api";
import { Layout } from "./components/Layout";
import { RouteError } from "./components/RouteError";
import { Loading } from "./components/Status";
import { NotFoundPage } from "./pages/NotFoundPage";
import { RequireAuth } from "./auth/RequireAuth";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000,
      // Client errors (404, 400, 429) will not change on retry.
      retry: (failureCount, error) =>
        !(error instanceof ApiError && error.status < 500) && failureCount < 2,
    },
  },
});

// Each page is its own code file, loaded the first time it is opened, so the first visit downloads less.
const router = createBrowserRouter([
  {
    element: <Layout />,
    errorElement: <RouteError />,
    // Shown for the moment the first page's code is loading.
    hydrateFallbackElement: <Loading />,
    children: [
      {
        // Errors inside a page show here, so the header and footer stay usable.
        errorElement: <RouteError />,
        children: [
          { index: true, lazy: async () => ({ Component: (await import("./pages/HomePage")).HomePage }) },
          { path: "activity", lazy: async () => ({ Component: (await import("./pages/ActivityPage")).ActivityPage }) },
          { path: "movie/:tmdbId", lazy: async () => ({ Component: (await import("./pages/MoviePage")).MoviePage }) },
          { path: "person/:personId", lazy: async () => ({ Component: (await import("./pages/PersonPage")).PersonPage }) },
          { path: "list/:listId", lazy: async () => ({ Component: (await import("./pages/ListPage")).ListPage }) },
          { path: "review/:reviewId", lazy: async () => ({ Component: (await import("./pages/ReviewPage")).ReviewPage }) },
          { path: "search", lazy: async () => ({ Component: (await import("./pages/SearchPage")).SearchPage }) },
          { path: "user/:userId", lazy: async () => ({ Component: (await import("./pages/ProfilePage")).UserPage }) },
          { path: "login", lazy: async () => ({ Component: (await import("./pages/AuthPages")).LoginPage }) },
          { path: "register", lazy: async () => ({ Component: (await import("./pages/AuthPages")).RegisterPage }) },
          { path: "confirm-email", lazy: async () => ({ Component: (await import("./pages/AuthPages")).ConfirmEmailPage }) },
          { path: "forgot-password", lazy: async () => ({ Component: (await import("./pages/AuthPages")).ForgotPasswordPage }) },
          { path: "reset-password", lazy: async () => ({ Component: (await import("./pages/AuthPages")).ResetPasswordPage }) },
          {
            element: <RequireAuth />,
            children: [
              { path: "profile", lazy: async () => ({ Component: (await import("./pages/ProfilePage")).ProfilePage }) },
              { path: "settings", lazy: async () => ({ Component: (await import("./pages/SettingsPage")).SettingsPage }) },
            ],
          },
          { path: "*", element: <NotFoundPage /> },
        ],
      },
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
