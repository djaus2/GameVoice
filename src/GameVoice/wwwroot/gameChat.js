// GameChat - text chat helpers shared by the games (in-game chat and lobby chat).
window.GameChat = window.GameChat || {};

window.GameChat.scrollToBottom = function (el) {
  try {
    if (!el) return;
    el.scrollTop = el.scrollHeight;
  } catch (e) {
  }
};

// Calls dotNetRef.<methodName>() (default OnChatEnterFromJs) when Enter is pressed in el.
window.GameChat.wireEnterToSend = function (el, dotNetRef, methodName) {
  try {
    if (!el || !dotNetRef) return;
    if (el.__gameChatEnterWired) return;
    el.__gameChatEnterWired = true;
    var method = methodName || 'OnChatEnterFromJs';
    el.addEventListener('keydown', function (ev) {
      try {
        if (ev.key === 'Enter') {
          ev.preventDefault();
          dotNetRef.invokeMethodAsync(method);
        }
      } catch (e) {
      }
    });
  } catch (e) {
  }
};
