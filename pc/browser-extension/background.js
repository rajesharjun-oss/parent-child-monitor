const HOST_NAME = "com.childscreentime.browser";

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (!message || message.type !== "youtubePlayback" || !sender.tab) {
    return false;
  }

  chrome.runtime.sendNativeMessage(HOST_NAME, message, (response) => {
    if (chrome.runtime.lastError) {
      sendResponse({ ok: false, error: "native_host_unavailable" });
      return;
    }
    sendResponse(response ?? { ok: false, error: "empty_response" });
  });

  return true;
});
