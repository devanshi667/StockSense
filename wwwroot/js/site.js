/* StockSense - shared behaviour: mobile sidebar, confirmation dialog, double-submit guard, auto-submit filters, print. */
(function () {
    'use strict';

    var body = document.body;

    /* ---------- Responsive sidebar ---------- */
    var toggle = document.getElementById('menuToggle');
    var backdrop = document.getElementById('sidebarBackdrop');

    function setSidebar(open) {
        body.classList.toggle('sidebar-open', open);
        if (toggle) {
            toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
            toggle.setAttribute('aria-label', open ? 'Close menu' : 'Open menu');
        }
    }

    if (toggle) {
        toggle.addEventListener('click', function () {
            setSidebar(!body.classList.contains('sidebar-open'));
        });
    }
    if (backdrop) {
        backdrop.addEventListener('click', function () { setSidebar(false); });
    }
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') { setSidebar(false); }
    });
    Array.prototype.forEach.call(document.querySelectorAll('.sidebar .side-link'), function (link) {
        link.addEventListener('click', function () { setSidebar(false); });
    });
    window.addEventListener('resize', function () {
        if (window.innerWidth >= 992) { setSidebar(false); }
    });

    /* ---------- Confirmation dialog for forms with data-confirm ---------- */
    var confirmEl = document.getElementById('confirmModal');
    var confirmOk = document.getElementById('confirmOk');
    var pendingForm = null;

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form || !form.getAttribute) { return; }

        var message = form.getAttribute('data-confirm');
        if (!message || form.getAttribute('data-confirmed') === 'true') { return; }

        if (!confirmEl || !window.bootstrap || !confirmOk) {
            if (!window.confirm(message)) { e.preventDefault(); }
            return;
        }

        e.preventDefault();
        pendingForm = form;

        document.getElementById('confirmTitle').textContent = form.getAttribute('data-confirm-title') || 'Please confirm';
        document.getElementById('confirmBody').textContent = message;
        confirmOk.textContent = form.getAttribute('data-confirm-button') || 'Yes, continue';
        confirmOk.className = 'btn ' + (form.getAttribute('data-confirm-class') || 'btn-danger');

        window.bootstrap.Modal.getOrCreateInstance(confirmEl).show();
    });

    if (confirmOk) {
        confirmOk.addEventListener('click', function () {
            if (!pendingForm) { return; }
            var form = pendingForm;
            pendingForm = null;
            form.setAttribute('data-confirmed', 'true');
            window.bootstrap.Modal.getOrCreateInstance(confirmEl).hide();
            disableSubmitButtons(form);
            form.submit();
        });
    }

    /* ---------- Stop double submits (the server also rejects duplicates) ---------- */
    function disableSubmitButtons(form) {
        Array.prototype.forEach.call(form.querySelectorAll('button[type=submit], input[type=submit]'), function (b) {
            b.disabled = true;
        });
    }

    function enableSubmitButtons(form) {
        Array.prototype.forEach.call(form.querySelectorAll('button[type=submit], input[type=submit]'), function (b) {
            b.disabled = false;
        });
    }

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form || !form.hasAttribute || !form.hasAttribute('data-once') || e.defaultPrevented) { return; }
        // Disable after the browser has collected the form data.
        setTimeout(function () { disableSubmitButtons(form); }, 0);
    });

    // Coming back with the browser Back button must not leave the buttons disabled.
    window.addEventListener('pageshow', function (e) {
        if (e.persisted) {
            Array.prototype.forEach.call(document.querySelectorAll('form[data-once]'), enableSubmitButtons);
        }
    });

    /* ---------- Filters that apply as soon as a dropdown changes ---------- */
    Array.prototype.forEach.call(document.querySelectorAll('[data-autosubmit]'), function (el) {
        el.addEventListener('change', function () {
            if (el.form) { el.form.submit(); }
        });
    });

    /* ---------- Print buttons ---------- */
    Array.prototype.forEach.call(document.querySelectorAll('[data-print]'), function (btn) {
        btn.addEventListener('click', function () { window.print(); });
    });
})();
