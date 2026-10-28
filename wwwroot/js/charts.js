/* StockSense - Chart.js helpers. Data is passed from the server as JSON, never hard-coded. */
window.StockSense = window.StockSense || {};

(function (S) {
    'use strict';

    var SALES = '#10b981';      // emerald
    var SALES_DARK = '#059669';
    var PROFIT = '#b45309';     // amber, high contrast against emerald
    var GRID = 'rgba(6, 78, 59, 0.08)';

    S.readJson = function (id) {
        var el = document.getElementById(id);
        if (!el) { return []; }
        try { return JSON.parse(el.textContent || '[]'); } catch (e) { return []; }
    };

    function rupees(value) {
        return '₹' + Number(value || 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function compact(value) {
        var v = Number(value || 0);
        var abs = Math.abs(v);
        if (abs >= 10000000) { return '₹' + (v / 10000000).toFixed(1).replace(/\.0$/, '') + 'Cr'; }
        if (abs >= 100000) { return '₹' + (v / 100000).toFixed(1).replace(/\.0$/, '') + 'L'; }
        if (abs >= 1000) { return '₹' + (v / 1000).toFixed(1).replace(/\.0$/, '') + 'k'; }
        return '₹' + v;
    }

    function context(canvasId) {
        var canvas = document.getElementById(canvasId);
        if (!canvas) { return null; }
        if (typeof Chart === 'undefined') {
            var note = document.createElement('div');
            note.className = 'empty-inline';
            note.textContent = 'The chart library could not be loaded (check your internet connection).';
            canvas.parentNode.replaceChild(note, canvas);
            return null;
        }
        Chart.defaults.font.family = "'Inter', system-ui, sans-serif";
        Chart.defaults.color = '#4b5563';
        return canvas.getContext('2d');
    }

    var baseOptions = {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: { position: 'bottom', labels: { usePointStyle: true, boxWidth: 8 } },
            tooltip: {
                callbacks: {
                    label: function (item) { return ' ' + item.dataset.label + ': ' + rupees(item.parsed.y); }
                }
            }
        },
        scales: {
            x: { grid: { display: false } },
            y: { beginAtZero: true, grid: { color: GRID }, ticks: { callback: function (v) { return compact(v); } } }
        }
    };

    function clone(obj) { return JSON.parse(JSON.stringify(obj)); }

    /** Bars = sales, line = gross profit. rows: [{label, sales, profit}] */
    S.salesProfitChart = function (canvasId, rows) {
        var ctx = context(canvasId);
        if (!ctx) { return null; }
        var options = clone(baseOptions);
        options.plugins.tooltip.callbacks = { label: function (item) { return ' ' + item.dataset.label + ': ' + rupees(item.parsed.y); } };
        options.scales.y.ticks = { callback: function (v) { return compact(v); } };

        return new Chart(ctx, {
            type: 'bar',
            data: {
                labels: rows.map(function (r) { return r.label; }),
                datasets: [
                    {
                        type: 'bar', label: 'Sales', data: rows.map(function (r) { return r.sales; }),
                        backgroundColor: SALES, hoverBackgroundColor: SALES_DARK, borderRadius: 6, maxBarThickness: 46, order: 2
                    },
                    {
                        type: 'line', label: 'Gross profit', data: rows.map(function (r) { return r.profit; }),
                        borderColor: PROFIT, backgroundColor: PROFIT, borderWidth: 3, tension: 0.3,
                        pointRadius: 4, pointHoverRadius: 6, order: 1
                    }
                ]
            },
            options: options
        });
    };

    /** Simple bar chart of sales. rows: [{label, sales}] */
    S.salesBarChart = function (canvasId, rows) {
        var ctx = context(canvasId);
        if (!ctx) { return null; }
        var options = clone(baseOptions);
        options.plugins.legend = { display: false };
        options.plugins.tooltip.callbacks = { label: function (item) { return ' Sales: ' + rupees(item.parsed.y); } };
        options.scales.y.ticks = { callback: function (v) { return compact(v); } };

        return new Chart(ctx, {
            type: 'bar',
            data: {
                labels: rows.map(function (r) { return r.label; }),
                datasets: [{ label: 'Sales', data: rows.map(function (r) { return r.sales; }), backgroundColor: SALES, hoverBackgroundColor: SALES_DARK, borderRadius: 5, maxBarThickness: 28 }]
            },
            options: options
        });
    };
})(window.StockSense);
