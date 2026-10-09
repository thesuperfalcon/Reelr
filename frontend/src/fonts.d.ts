// Fontsource packages export each weight as a CSS file without the ".css" extension
// (e.g. "@fontsource/instrument-sans/400"), which vite/client's "*.css" declaration does not cover.
declare module "@fontsource/*";
