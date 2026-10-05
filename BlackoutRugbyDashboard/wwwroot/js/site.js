// Dashboard 2.0 client-side enhancement: in-page navigation.
//
// The Squad page's controls (view / mode / Stat-Group tabs, column sorts, the
// game / stat / comparison pickers) are query-string links and GET forms that
// re-render the same page. Without help, every one of them is a full reload that
// throws you back to the top. This intercepts same-path GET navigations, fetches
// the new page, swaps only the #page-content region, and pushes history — so the
// card you were reading stays put. Cross-page links and POSTs are untouched, and
// everything still works with this file absent (real links, real forms).
(function () {
  'use strict';

  var MAIN_ID = 'page-content';
  var swapping = false;

  // Re-create inline scripts so the swapped region's own JS (e.g. the debug
  // panel) re-initialises; innerHTML alone would leave them inert.
  function runScripts(root) {
    root.querySelectorAll('script').forEach(function (old) {
      var script = document.createElement('script');
      for (var i = 0; i < old.attributes.length; i++) {
        script.setAttribute(old.attributes[i].name, old.attributes[i].value);
      }
      script.textContent = old.textContent;
      old.replaceWith(script);
    });
  }

  function swap(url, push) {
    if (swapping) {
      return;
    }
    var main = document.getElementById(MAIN_ID);
    if (!main) {
      location.href = url;
      return;
    }

    swapping = true;
    main.classList.add('is-loading');

    fetch(url, { credentials: 'same-origin' })
      .then(function (response) {
        if (!response.ok) {
          throw new Error('HTTP ' + response.status);
        }
        return response.text();
      })
      .then(function (html) {
        var doc = new DOMParser().parseFromString(html, 'text/html');
        var next = doc.getElementById(MAIN_ID);
        if (!next) {
          throw new Error('no ' + MAIN_ID + ' in response');
        }
        main.replaceWith(next);
        runScripts(next);
        if (push) {
          history.pushState({ swap: true }, '', url);
        }
      })
      .catch(function () {
        // Any failure falls back to a normal navigation — never a dead click.
        location.href = url;
      })
      .finally(function () {
        swapping = false;
      });
  }

  // Same-page link clicks (view / mode / tab / sort / team-tab): swap in place.
  document.addEventListener('click', function (event) {
    if (event.defaultPrevented || event.button !== 0
      || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }

    var link = event.target.closest('a[href]');
    if (!link || (link.target && link.target !== '_self')) {
      return;
    }

    var href = link.getAttribute('href');
    if (!href || href.charAt(0) === '#') {
      return;
    }

    var url = new URL(link.href, location.href);
    if (url.origin !== location.origin || url.pathname !== location.pathname) {
      return;
    }

    event.preventDefault();
    swap(url.pathname + url.search + url.hash, true);
  });

  // Same-page GET forms (game / stat / comparison pickers): swap in place.
  document.addEventListener('submit', function (event) {
    // A page whose own handler already cancelled the submit (e.g. the
    // Playground's onsubmit="return false") must be left alone.
    if (event.defaultPrevented) {
      return;
    }

    var form = event.target;
    if (!(form instanceof HTMLFormElement)) {
      return;
    }
    if ((form.getAttribute('method') || 'get').toLowerCase() !== 'get') {
      return;
    }

    var action = form.getAttribute('action') || location.href;
    var url = new URL(action, location.href);
    if (url.origin !== location.origin || url.pathname !== location.pathname) {
      return;
    }

    event.preventDefault();
    var query = new URLSearchParams(new FormData(form)).toString();
    swap(url.pathname + (query ? '?' + query : ''), true);
  });

  // Pickers marked data-autosubmit submit their form on change (requestSubmit
  // fires the submit event above; a bare form.submit() would bypass it).
  document.addEventListener('change', function (event) {
    var select = event.target;
    if (!(select instanceof HTMLSelectElement) || !select.hasAttribute('data-autosubmit')) {
      return;
    }
    if (select.form) {
      if (typeof select.form.requestSubmit === 'function') {
        select.form.requestSubmit();
      } else {
        select.form.submit();
      }
    }
  });

  window.addEventListener('popstate', function () {
    swap(location.pathname + location.search, false);
  });
})();