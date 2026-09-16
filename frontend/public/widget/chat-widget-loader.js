/**
 * Live chat widget loader (Communication Channels / Live chat, CS-303).
 *
 * Embed with a single script tag:
 *   <script src="https://<app-origin>/widget/chat-widget-loader.js"
 *           data-account="<channelAccountId>" data-lang="en"></script>
 *
 * The loader itself never touches the host page's styles or DOM beyond the launcher button and
 * the iframe it creates — the chat UI lives entirely inside that iframe, on this app's own origin,
 * so it never inherits the embedding page's CSS.
 */
(function () {
  var currentScript = document.currentScript;
  if (!currentScript) return;

  var accountId = currentScript.getAttribute('data-account');
  if (!accountId) {
    console.error('[chat-widget] missing required data-account attribute.');
    return;
  }

  var lang = currentScript.getAttribute('data-lang') || 'en';
  var origin = new URL(currentScript.src).origin;

  var launcher = document.createElement('button');
  launcher.setAttribute('type', 'button');
  launcher.setAttribute('aria-label', 'Open chat');
  launcher.textContent = '💬';
  launcher.style.cssText =
    'position:fixed;inset-inline-end:20px;inset-block-end:20px;width:56px;height:56px;' +
    'border-radius:9999px;border:none;background:#0f766e;color:#fff;font-size:24px;' +
    'box-shadow:0 4px 12px rgba(0,0,0,0.2);cursor:pointer;z-index:2147483000;';

  var frame = document.createElement('iframe');
  frame.src = origin + '/widget/chat?account=' + encodeURIComponent(accountId) + '&lang=' + encodeURIComponent(lang);
  frame.title = 'Live chat';
  frame.style.cssText =
    'position:fixed;inset-inline-end:20px;inset-block-end:88px;width:360px;height:520px;' +
    'max-height:70vh;border:none;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,0.25);' +
    'z-index:2147483000;display:none;';

  var open = false;

  function setOpen(next) {
    open = next;
    frame.style.display = open ? 'block' : 'none';
  }

  launcher.addEventListener('click', function () {
    setOpen(!open);
  });

  window.addEventListener('message', function (event) {
    if (event.origin !== origin) return;
    var data = event.data || {};
    if (data.type === 'chat-widget:close') {
      setOpen(false);
    }
  });

  document.body.appendChild(frame);
  document.body.appendChild(launcher);
})();
