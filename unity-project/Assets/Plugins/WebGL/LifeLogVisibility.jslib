mergeInto(LibraryManager.library, {
  HalkaLifeLogTabVisible: function () {
    return document.visibilityState === 'visible' ? 1 : 0;
  }
});
