mergeInto(LibraryManager.library, {
  // 1 when the game runs inside the wedding page (which owns the full-screen mode), else 0
  IsInWebsite: function () {
    try {
      return (window.parent && window.parent !== window && typeof window.parent.deactivateFullScreen === 'function') ? 1 : 0;
    } catch (e) { return 0; }
  },
  BackToWebsite: function () {
    try {
      if (window.parent && window.parent !== window && typeof window.parent.deactivateFullScreen === 'function') window.parent.deactivateFullScreen();
    } catch (e) { console.warn(e); }
  },
  GameOverAndSendScore: function (score) {
    console.log("Unityからスコアを受け取りました: ", score);
    if (window.parent && typeof window.parent.ReceiveScoreFromUnity === 'function') {
      window.parent.ReceiveScoreFromUnity(score);
    } else if (typeof window.ReceiveScoreFromUnity === 'function') {
      window.ReceiveScoreFromUnity(score);
    }
  }
});