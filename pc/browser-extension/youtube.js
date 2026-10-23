(() => {
  const FLUSH_INTERVAL_MS = 10000;
  const MAX_SECONDS_PER_MESSAGE = 30;
  const VIDEO_ID_PATTERN = /^[A-Za-z0-9_-]{11}$/;

  let activeVideo = null;
  let activeVideoId = "";
  let activeStartedAt = null;
  let accumulatedSeconds = 0;

  function safeText(value, maxLength) {
    return (value ?? "").replace(/[\u0000-\u001F\u007F]/g, "").trim().slice(0, maxLength);
  }

  function getVideoId() {
    try {
      const value = new URL(window.location.href).searchParams.get("v") ?? "";
      return VIDEO_ID_PATTERN.test(value) ? value : "";
    } catch {
      return "";
    }
  }

  function getTitle() {
    const heading = document.querySelector("ytd-watch-metadata h1 yt-formatted-string");
    const text = safeText(heading?.textContent, 200);
    if (text) {
      return text;
    }
    return safeText(document.title.replace(/\s*-\s*YouTube\s*$/i, ""), 200);
  }

  function getChannel() {
    const channel = document.querySelector("ytd-watch-metadata ytd-channel-name a, ytd-watch-metadata #owner-name a");
    return safeText(channel?.textContent, 120);
  }

  function isCounting() {
    return activeVideo &&
      !document.hidden &&
      !activeVideo.paused &&
      !activeVideo.ended &&
      activeVideo.readyState >= 2 &&
      Boolean(activeVideoId);
  }

  function settleClock() {
    const now = performance.now();
    if (activeStartedAt !== null) {
      accumulatedSeconds += Math.max(0, (now - activeStartedAt) / 1000);
      activeStartedAt = null;
    }
    if (isCounting()) {
      activeStartedAt = now;
    }
  }

  function sendAccumulated() {
    settleClock();
    if (!activeVideoId || accumulatedSeconds < 1) {
      return;
    }

    const wholeSeconds = Math.min(MAX_SECONDS_PER_MESSAGE, Math.floor(accumulatedSeconds));
    const title = getTitle();
    if (!title) {
      return;
    }

    accumulatedSeconds -= wholeSeconds;
    chrome.runtime.sendMessage({
      type: "youtubePlayback",
      videoId: activeVideoId,
      title,
      channel: getChannel(),
      watchedSeconds: wholeSeconds
    });
  }

  function detachVideo() {
    if (!activeVideo) {
      return;
    }
    activeVideo.removeEventListener("play", settleClock);
    activeVideo.removeEventListener("pause", settleClock);
    activeVideo.removeEventListener("ended", settleClock);
    activeVideo = null;
  }

  function refreshVideo() {
    const nextVideoId = getVideoId();
    const nextVideo = document.querySelector("video");

    if (nextVideoId !== activeVideoId) {
      sendAccumulated();
      accumulatedSeconds = 0;
      activeVideoId = nextVideoId;
      activeStartedAt = null;
    }

    if (nextVideo !== activeVideo) {
      settleClock();
      detachVideo();
      activeVideo = nextVideo;
      if (activeVideo) {
        activeVideo.addEventListener("play", settleClock);
        activeVideo.addEventListener("pause", settleClock);
        activeVideo.addEventListener("ended", settleClock);
      }
      settleClock();
    }
  }

  document.addEventListener("visibilitychange", () => {
    settleClock();
    if (!document.hidden) {
      refreshVideo();
    }
  });

  window.addEventListener("pagehide", sendAccumulated);
  window.addEventListener("yt-navigate-start", sendAccumulated);
  window.addEventListener("yt-navigate-finish", refreshVideo);

  setInterval(refreshVideo, 1000);
  setInterval(sendAccumulated, FLUSH_INTERVAL_MS);
  refreshVideo();
})();
