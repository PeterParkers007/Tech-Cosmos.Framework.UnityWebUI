(function () {
  'use strict';

  function post(payload) {
    var json = JSON.stringify(payload);
    if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
      window.chrome.webview.postMessage(json);
      return;
    }
    if (window.vuplex && typeof window.vuplex.postMessage === 'function') {
      window.vuplex.postMessage(json);
      return;
    }
    if (window.Unity && typeof window.Unity.call === 'function') {
      window.Unity.call(json);
      return;
    }
    console.warn('[UnityWebUI] WebView bridge is not connected.', payload);
  }

  if (!window.Unity) {
    window.Unity = {
      call: function (msg) {
        window.location = 'unity:' + msg;
      }
    };
  }

  function stripTags(text) {
    return (text || '').replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim();
  }

  function isCjk(ch) {
    var code = ch.charCodeAt(0);
    return code >= 0x4e00 && code <= 0x9fff;
  }

  function slugFromText(text) {
    text = stripTags(text);
    if (!text) return '';

    var slug = '';
    var lastWasSeparator = false;
    for (var i = 0; i < text.length; i++) {
      var ch = text.charAt(i);
      var code = text.charCodeAt(i);
      var isWord = (code >= 48 && code <= 57) ||
        (code >= 65 && code <= 90) ||
        (code >= 97 && code <= 122) ||
        isCjk(ch);
      if (isWord) {
        slug += ch;
        lastWasSeparator = false;
      } else if (!lastWasSeparator && slug.length > 0) {
        slug += '-';
        lastWasSeparator = true;
      }
    }

    slug = slug.replace(/^-+|-+$/g, '');
    if (slug.length > 32) slug = slug.substring(0, 32).replace(/-+$/g, '');
    return slug;
  }

  function resolveButtonActionIds(buttons) {
    var slugCounts = {};
    var ids = new Array(buttons.length);
    for (var i = 0; i < buttons.length; i++) {
      var btn = buttons[i];
      var explicit = btn.getAttribute('data-unity-action');
      if (explicit) {
        ids[i] = explicit.trim();
        continue;
      }
      if (btn.id) {
        ids[i] = btn.id.trim();
        continue;
      }

      var slug = slugFromText(btn.textContent || '');
      if (slug) {
        if (!slugCounts[slug]) {
          slugCounts[slug] = 1;
          ids[i] = slug;
        } else {
          slugCounts[slug]++;
          ids[i] = slug + '-' + slugCounts[slug];
        }
        continue;
      }

      ids[i] = 'button-' + i;
    }
    return ids;
  }

  function resolveButtonActionId(btn) {
    var buttons = Array.prototype.slice.call(document.querySelectorAll('button'));
    var index = buttons.indexOf(btn);
    if (index < 0) return 'button';
    return resolveButtonActionIds(buttons)[index];
  }

  window.__unityWebUiEmitActionForElement = function (startEl) {
    if (!startEl || !startEl.closest) return false;

    var actionEl = startEl.closest('[data-unity-action]');
    if (actionEl) {
      post({ type: 'action', id: actionEl.getAttribute('data-unity-action') });
      return true;
    }

    var button = startEl.closest('button');
    if (button) {
      post({ type: 'action', id: resolveButtonActionId(button) });
      return true;
    }

    return false;
  };

  window.__unityWebUiEmitActionAtPoint = function (x, y) {
    var el = document.elementFromPoint(x, y);
    if (!el)
      return false;

    el.dispatchEvent(new MouseEvent('click', {
      bubbles: true,
      cancelable: true,
      view: window,
      clientX: x,
      clientY: y,
      button: 0
    }));

    return window.__unityWebUiEmitActionForElement(el);
  };

  // GPU native plugin injects an older inline bridge on document created.
  if (!window.__unityWebUiGpuBridge) {
    document.addEventListener('click', function (event) {
      if (window.__unityWebUiEmitActionForElement(event.target)) {
        event.preventDefault();
        return;
      }

      var link = event.target.closest('a[href]');
      if (!link || link.hasAttribute('data-unity-action')) return;

      var href = link.getAttribute('href') || '';
      if (href.charAt(0) === '#') return;

      event.preventDefault();
      post({ type: 'navigate', href: href });
    }, true);
  }
})();
