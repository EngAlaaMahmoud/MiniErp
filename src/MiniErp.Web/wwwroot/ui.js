(function () {
  const THEME_KEY = "miniErp.theme";

  function normalizeTheme(theme) {
    const t = (theme || "").toString().toLowerCase();
    if (t === "dark" || t === "ocean" || t === "light") return t;
    return "light";
  }

  function applyTheme(theme) {
    const t = normalizeTheme(theme);
    document.documentElement.setAttribute("data-theme", t);
    try {
      localStorage.setItem(THEME_KEY, t);
    } catch {
      // ignore
    }
    return t;
  }

  function getTheme() {
    try {
      return normalizeTheme(localStorage.getItem(THEME_KEY));
    } catch {
      return "light";
    }
  }

  window.miniErpUi = {
    getTheme,
    setTheme: applyTheme,
    initTheme: function () {
      applyTheme(getTheme());
    },
  };

  window.miniErpUi.initTheme();
})();

