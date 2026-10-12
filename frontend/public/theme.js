// Applies the look chosen in Settings before the page first paints, so a light look never flashes dark.
// It is a separate file because the Content-Security-Policy allows no inline scripts. Keep the key,
// the names and the colours in step with src/lib/theme.ts.
(function () {
  var backgrounds = { broadsheet: "#f3efe6", marquee: "#0c0c0e", "video-store": "#fff3d4", noir: "#0a0a0a" };
  try {
    var theme = localStorage.getItem("reelr.theme");
    if (theme && backgrounds[theme]) {
      document.documentElement.dataset.theme = theme;
      document.querySelector('meta[name="theme-color"]').setAttribute("content", backgrounds[theme]);
    }
  } catch (e) {
    // Storage can be blocked; Salon is shown then.
  }
})();
