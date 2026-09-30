mergeInto(LibraryManager.library, {
  HalkaTouchUiMode: function () {
    if (new URLSearchParams(window.location.search).get('touchControls') === '1') return 2;
    return navigator.maxTouchPoints > 0 &&
      window.matchMedia && window.matchMedia('(pointer: coarse)').matches ? 1 : 0;
  }
});
