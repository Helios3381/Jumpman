mergeInto(LibraryManager.library, {
  GameOverAndSendScore: function (score) {
    console.log("Unityからスコアを受け取りました: ", score);
    if (window.parent && typeof window.parent.ReceiveScoreFromUnity === 'function') {
      window.parent.ReceiveScoreFromUnity(score);
    } else if (typeof window.ReceiveScoreFromUnity === 'function') {
      window.ReceiveScoreFromUnity(score);
    }
  }
});