/* StockSense - item rows for the New sale and Record purchase forms.
   Reads its data from <script id="lineConfig"> (products + rows to restore after a server-side validation error).
   Totals shown here are a preview only: the server recalculates everything from saved product prices. */
(function () {
    'use strict';

    var form = document.getElementById('lineForm');
    var cfgEl = document.getElementById('lineConfig');
    var body = document.getElementById('lineBody');
    if (!form || !cfgEl || !body) { return; }

    var cfg;
    try { cfg = JSON.parse(cfgEl.textContent); } catch (e) { return; }

    var mode = cfg.mode === 'purchase' ? 'purchase' : 'sale';
    var products = cfg.products || [];
    var byId = {};
    products.forEach(function (p) { byId[String(p.id)] = p; });

    var errorsBox = document.getElementById('clientErrors');

    /* ---------- helpers ---------- */
    function num(value) {
        var n = parseFloat(value);
        return isNaN(n) ? 0 : n;
    }
    function round2(n) { return Math.round((n + Number.EPSILON) * 100) / 100; }
    function money(n) {
        return '₹' + Number(n || 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
    function qtyText(n) {
        return Number(n || 0).toLocaleString('en-IN', { maximumFractionDigits: 3 });
    }
    function cell(className) {
        var td = document.createElement('td');
        if (className) { td.className = className; }
        return td;
    }
    function rows() {
        return Array.prototype.slice.call(body.querySelectorAll('tr.line-row'));
    }
    function $(id) { return document.getElementById(id); }

    /* ---------- row construction ---------- */
    function buildRow(line) {
        line = line || {};
        var tr = document.createElement('tr');
        tr.className = 'line-row';

        // Product
        var tdProduct = cell();
        var select = document.createElement('select');
        select.className = 'form-select line-product';
        select.setAttribute('data-field', 'ProductId');
        select.setAttribute('aria-label', 'Product');
        select.appendChild(new Option('Select a product...', ''));
        products.forEach(function (p) {
            var label = p.sku + ' · ' + p.name;
            if (mode === 'sale') { label += ' (' + qtyText(p.stock) + ' ' + p.unit + ' in stock)'; }
            select.appendChild(new Option(label, String(p.id)));
        });
        var hint = document.createElement('div');
        hint.className = 'line-hint';
        tdProduct.appendChild(select);
        tdProduct.appendChild(hint);
        tr.appendChild(tdProduct);

        // Quantity
        var tdQty = cell();
        var qty = document.createElement('input');
        qty.type = 'number';
        qty.step = 'any';
        qty.min = '0';
        qty.className = 'form-control line-qty';
        qty.setAttribute('data-field', 'Quantity');
        qty.setAttribute('inputmode', 'decimal');
        qty.setAttribute('aria-label', 'Quantity');
        tdQty.appendChild(qty);
        tr.appendChild(tdQty);

        if (mode === 'sale') {
            var tdPrice = cell('text-end text-nowrap');
            var price = document.createElement('span');
            price.className = 'line-price';
            price.textContent = '-';
            tdPrice.appendChild(price);
            tr.appendChild(tdPrice);
        } else {
            var tdCost = cell();
            var cost = document.createElement('input');
            cost.type = 'number';
            cost.step = '0.01';
            cost.min = '0';
            cost.className = 'form-control line-cost';
            cost.setAttribute('data-field', 'UnitCost');
            cost.setAttribute('inputmode', 'decimal');
            cost.setAttribute('aria-label', 'Unit cost');
            tdCost.appendChild(cost);
            tr.appendChild(tdCost);

            var tdExpiry = cell();
            var expiry = document.createElement('input');
            expiry.type = 'date';
            expiry.className = 'form-control line-expiry';
            expiry.setAttribute('data-field', 'ExpiryDate');
            expiry.setAttribute('aria-label', 'Batch expiry date');
            tdExpiry.appendChild(expiry);
            tr.appendChild(tdExpiry);
        }

        // Line total
        var tdTotal = cell('text-end text-nowrap fw-semibold');
        var total = document.createElement('span');
        total.className = 'line-total';
        total.textContent = money(0);
        tdTotal.appendChild(total);
        tr.appendChild(tdTotal);

        // Remove
        var tdRemove = cell('text-end');
        var remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'btn btn-sm btn-light text-danger remove-line';
        remove.setAttribute('aria-label', 'Remove this item');
        remove.innerHTML = '<i class="bi bi-x-lg"></i>';
        tdRemove.appendChild(remove);
        tr.appendChild(tdRemove);

        // Restore values (after a server-side validation error) or defaults
        if (line.productId !== undefined && line.productId !== null && byId[String(line.productId)]) {
            select.value = String(line.productId);
        }
        qty.value = line.quantity !== undefined && line.quantity !== null ? String(line.quantity) : '';
        if (mode === 'purchase') {
            var p = byId[select.value];
            var costValue = line.unitCost;
            if ((costValue === undefined || costValue === null || Number(costValue) === 0) && p) { costValue = p.cost; }
            tr.querySelector('.line-cost').value = costValue ? String(costValue) : '';
            tr.querySelector('.line-expiry').value = line.expiryDate || '';
        }

        return tr;
    }

    function addRow(line, focus) {
        var tr = buildRow(line);
        body.appendChild(tr);
        reindex();
        recalc();
        if (focus) { tr.querySelector('.line-product').focus(); }
        return tr;
    }

    /** Names must be Lines[0], Lines[1]... without gaps so ASP.NET Core binds the whole list. */
    function reindex() {
        rows().forEach(function (tr, i) {
            Array.prototype.forEach.call(tr.querySelectorAll('[data-field]'), function (input) {
                input.name = 'Lines[' + i + '].' + input.getAttribute('data-field');
            });
        });
    }

    /* ---------- calculations ---------- */
    function lineInfo(tr) {
        var product = byId[tr.querySelector('.line-product').value] || null;
        var qty = num(tr.querySelector('.line-qty').value);
        var unit;
        if (mode === 'sale') {
            unit = product ? num(product.price) : 0;
        } else {
            unit = num(tr.querySelector('.line-cost').value);
        }
        return { product: product, qty: qty, unit: unit, total: round2(qty * unit) };
    }

    function recalc() {
        var subtotal = 0;

        rows().forEach(function (tr) {
            var info = lineInfo(tr);
            subtotal += info.total;

            tr.querySelector('.line-total').textContent = money(info.total);

            var hint = tr.querySelector('.line-hint');
            hint.className = 'line-hint';
            if (!info.product) {
                hint.textContent = '';
            } else if (mode === 'sale') {
                tr.querySelector('.line-price').textContent = money(info.unit) + ' / ' + info.product.unit;
                hint.textContent = 'Available: ' + qtyText(info.product.stock) + ' ' + info.product.unit;
                if (info.qty > num(info.product.stock)) {
                    hint.textContent = 'Only ' + qtyText(info.product.stock) + ' ' + info.product.unit + ' available';
                    hint.className = 'line-hint over';
                }
            } else {
                hint.textContent = 'In stock now: ' + qtyText(info.product.stock) + ' ' + info.product.unit;
            }
        });

        subtotal = round2(subtotal);

        if (mode === 'sale') {
            updateSaleSummary(subtotal);
        } else {
            var grand = $('grandTotal');
            if (grand) { grand.textContent = money(subtotal); }
        }
        return subtotal;
    }

    /* ---------- sale-only: discount, payment type, summary ---------- */
    var discountEl = $('Discount');
    var paidEl = $('AmountPaidNow');
    var customerEl = $('CustomerId');

    function isCredit() {
        var checked = form.querySelector('input[name="OnCredit"]:checked');
        return !!checked && checked.value === 'true';
    }

    function saleFigures(subtotal) {
        var discount = discountEl ? Math.max(num(discountEl.value), 0) : 0;
        var total = round2(subtotal - discount);
        var credit = isCredit();
        var paid = credit ? round2(Math.max(num(paidEl ? paidEl.value : 0), 0)) : total;
        return { discount: discount, total: total, credit: credit, paid: paid, due: round2(total - paid) };
    }

    function updateSaleSummary(subtotal) {
        var f = saleFigures(subtotal);
        var shownDiscount = Math.min(f.discount, subtotal);
        var shownTotal = Math.max(round2(subtotal - shownDiscount), 0);
        var shownPaid = f.credit ? Math.min(f.paid, shownTotal) : shownTotal;

        if ($('sumSubtotal')) { $('sumSubtotal').textContent = money(subtotal); }
        if ($('sumDiscount')) { $('sumDiscount').textContent = '-' + money(shownDiscount); }
        if ($('sumTotal')) { $('sumTotal').textContent = money(shownTotal); }
        if ($('sumPaid')) { $('sumPaid').textContent = money(shownPaid); }

        var due = round2(shownTotal - shownPaid);
        if ($('sumDue')) { $('sumDue').textContent = money(due); }
        if ($('sumDueRow')) { $('sumDueRow').classList.toggle('d-none', !(f.credit && due > 0)); }

        if ($('paidNowBox')) { $('paidNowBox').classList.toggle('d-none', !f.credit); }
        if ($('methodBox')) { $('methodBox').classList.toggle('d-none', f.credit && f.paid <= 0); }
        if ($('customerHint')) {
            $('customerHint').textContent = f.credit
                ? 'Required: choose the customer who owes this credit.'
                : 'Needed only if part or all of the bill goes on credit.';
        }
    }

    /* ---------- validation before submit (server validates again) ---------- */
    function validate() {
        var errors = [];
        var used = 0;
        var perProduct = {};

        rows().forEach(function (tr, i) {
            var info = lineInfo(tr);
            if (!info.product) { return; }
            used++;
            var label = 'Item ' + (i + 1) + ' (' + info.product.name + ')';

            if (info.qty <= 0) { errors.push(label + ': quantity must be greater than zero.'); }
            if (mode === 'sale') {
                perProduct[info.product.id] = (perProduct[info.product.id] || 0) + info.qty;
            } else if (info.unit <= 0) {
                errors.push(label + ': unit cost must be greater than zero.');
            }
        });

        if (used === 0) { errors.push('Add at least one product.'); }

        if (mode === 'sale') {
            Object.keys(perProduct).forEach(function (id) {
                var p = byId[id];
                if (p && perProduct[id] > num(p.stock)) {
                    errors.push('Not enough stock for ' + p.name + ': only ' + qtyText(p.stock) + ' ' + p.unit + ' available.');
                }
            });

            var subtotal = round2(rows().reduce(function (sum, tr) { return sum + lineInfo(tr).total; }, 0));
            var f = saleFigures(subtotal);

            if (f.discount > 0 && f.discount >= subtotal) { errors.push('The discount must be less than the subtotal.'); }
            if (f.credit) {
                if (!customerEl || !customerEl.value) { errors.push('Select a customer for a credit (udhaar) sale.'); }
                if (f.paid >= f.total && f.total > 0) { errors.push('For a credit sale the amount paid now must be less than the total. Choose "Paid in full" instead.'); }
            }
        } else {
            var supplier = $('SupplierId');
            if (supplier && !supplier.value) { errors.push('Please select a supplier.'); }
        }

        return errors;
    }

    function showErrors(errors) {
        if (!errorsBox) { return; }
        if (errors.length === 0) {
            errorsBox.classList.add('d-none');
            errorsBox.innerHTML = '';
            return;
        }
        errorsBox.innerHTML = '';
        var ul = document.createElement('ul');
        ul.className = 'mb-0 ps-3';
        errors.forEach(function (message) {
            var li = document.createElement('li');
            li.textContent = message;
            ul.appendChild(li);
        });
        errorsBox.appendChild(ul);
        errorsBox.classList.remove('d-none');
        errorsBox.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }

    /* ---------- events ---------- */
    body.addEventListener('change', function (e) {
        var target = e.target;
        if (target.classList.contains('line-product')) {
            var tr = target.closest('tr');
            var p = byId[target.value];
            var qty = tr.querySelector('.line-qty');
            if (p && (qty.value === '' || num(qty.value) <= 0)) { qty.value = '1'; }
            if (mode === 'purchase' && p) { tr.querySelector('.line-cost').value = String(p.cost); }
        }
        recalc();
    });
    body.addEventListener('input', recalc);

    body.addEventListener('click', function (e) {
        var btn = e.target.closest ? e.target.closest('.remove-line') : null;
        if (!btn) { return; }
        var tr = btn.closest('tr');
        if (rows().length <= 1) {
            // Keep one empty row instead of removing the last one.
            body.replaceChild(buildRow({}), tr);
        } else {
            body.removeChild(tr);
        }
        reindex();
        recalc();
    });

    var addBtn = $('addLine');
    if (addBtn) { addBtn.addEventListener('click', function () { addRow({}, true); }); }

    ['Discount', 'AmountPaidNow'].forEach(function (id) {
        var el = $(id);
        if (el) { el.addEventListener('input', recalc); }
    });
    Array.prototype.forEach.call(form.querySelectorAll('input[name="OnCredit"]'), function (radio) {
        radio.addEventListener('change', recalc);
    });

    form.addEventListener('submit', function (e) {
        var errors = validate();
        showErrors(errors);
        if (errors.length > 0) {
            e.preventDefault();
            return;
        }
        // Drop rows without a product so the posted list has no gaps.
        rows().forEach(function (tr) {
            if (!tr.querySelector('.line-product').value) { body.removeChild(tr); }
        });
        reindex();
    });

    /* ---------- quick "new customer" dialog (sale form only) ---------- */
    var modalEl = $('customerModal');
    if (modalEl && customerEl) {
        var saveBtn = $('qcSave');
        var errBox = $('customerErrors');

        saveBtn.addEventListener('click', function () {
            var fd = new FormData();
            fd.append('Name', $('qcName').value);
            fd.append('Mobile', $('qcMobile').value);
            fd.append('Address', $('qcAddress').value);
            var token = form.querySelector('input[name="__RequestVerificationToken"]');
            if (token) { fd.append('__RequestVerificationToken', token.value); }

            errBox.classList.add('d-none');
            saveBtn.disabled = true;

            fetch(modalEl.getAttribute('data-url'), {
                method: 'POST',
                body: fd,
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(function (response) {
                    return response.json().then(function (data) { return { ok: response.ok, data: data }; });
                })
                .then(function (result) {
                    if (result.ok) {
                        var option = new Option(result.data.label, String(result.data.id), true, true);
                        customerEl.appendChild(option);
                        customerEl.value = String(result.data.id);
                        ['qcName', 'qcMobile', 'qcAddress'].forEach(function (id) { $(id).value = ''; });
                        window.bootstrap.Modal.getOrCreateInstance(modalEl).hide();
                        recalc();
                    } else {
                        var messages = (result.data && result.data.errors) || ['The customer could not be saved.'];
                        errBox.textContent = messages.join(' ');
                        errBox.classList.remove('d-none');
                    }
                })
                .catch(function () {
                    errBox.textContent = 'The customer could not be saved. Please reload the page and try again.';
                    errBox.classList.remove('d-none');
                })
                .then(function () { saveBtn.disabled = false; });
        });
    }

    /* ---------- start ---------- */
    var initial = cfg.lines || [];
    if (initial.length === 0) {
        addRow({}, false);
    } else {
        initial.forEach(function (line) { addRow(line, false); });
    }
    recalc();
})();
