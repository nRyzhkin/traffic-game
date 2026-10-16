// Read-only query about the browser, used by TrafficGame.App.Platform.Browser.
mergeInto(LibraryManager.library, {
  TrafficGame_PrefersDarkScheme: function () {
    try {
      return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 1 : 0;
    } catch (e) {
      return 0;
    }
  }
});
