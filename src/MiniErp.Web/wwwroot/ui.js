(function () {
  const THEME_KEY = "miniErp.theme";
  const BODY_MODAL_OPEN_CLASS = "modal-open";
  const BODY_MODAL_OPEN_PADDING_KEY = "data-mini-erp-modal-padding";

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
    setBodyModalOpen: function (isOpen) {
      const open = !!isOpen;
      const body = document.body;
      if (!body) return;

      if (open) {
        if (!body.classList.contains(BODY_MODAL_OPEN_CLASS)) {
          const scrollbarWidth =
            window.innerWidth - document.documentElement.clientWidth;
          body.setAttribute(BODY_MODAL_OPEN_PADDING_KEY, body.style.paddingRight || "");
          body.classList.add(BODY_MODAL_OPEN_CLASS);
          if (scrollbarWidth > 0) {
            body.style.paddingRight = `${scrollbarWidth}px`;
          }
        }
        return;
      }

      if (body.classList.contains(BODY_MODAL_OPEN_CLASS)) {
        body.classList.remove(BODY_MODAL_OPEN_CLASS);
        const prevPadding = body.getAttribute(BODY_MODAL_OPEN_PADDING_KEY);
        body.style.paddingRight = prevPadding || "";
        body.removeAttribute(BODY_MODAL_OPEN_PADDING_KEY);
      }
    },
    initTheme: function () {
      applyTheme(getTheme());
    },
  };

  window.miniErpUi.initTheme();
})();
