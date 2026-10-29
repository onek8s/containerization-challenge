// Live quote on the packages step: posts the package lines whenever they change.
(function () {
    'use strict';
    var form = document.getElementById('packages-form');
    var target = document.getElementById('live-quote');
    if (!form || !target) { return; }
    var token = form.querySelector('input[name="__RequestVerificationToken"]').value;
    var timer = null;

    function readLines() {
        var lines = {};
        form.querySelectorAll('[name^="packages["]').forEach(function (el) {
            var m = /^packages\[(\d+)\]\.(\w+)$/.exec(el.name);
            if (!m) { return; }
            var line = lines[m[1]] || (lines[m[1]] = {});
            var value = el.type === 'checkbox' ? el.checked : el.value;
            if (el.tagName === 'SELECT' || ['Quantity', 'LengthCm', 'WidthCm', 'HeightCm'].indexOf(m[2]) >= 0) { value = parseInt(value, 10) || 0; }
            if (['WeightKg', 'DgNetQuantity'].indexOf(m[2]) >= 0) { value = value === '' ? null : parseFloat(String(value).replace(',', '.')); }
            line[m[2].charAt(0).toLowerCase() + m[2].slice(1)] = value;
        });
        return Object.keys(lines).map(function (k) { return lines[k]; });
    }

    function refresh() {
        fetch('/api/quote', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify(readLines())
        }).then(function (r) { return r.ok ? r.json() : []; }).then(function (quotes) {
            if (!quotes.length) { target.textContent = 'No price for this lane yet.'; return; }
            target.innerHTML = quotes.map(function (q) {
                return '<div>' + q.carrier + ': <strong>' + q.total.toFixed(2) + ' ' + q.currency + '</strong> (' + q.chargeableKg + ' kg, ' + q.transitDays + ' days)</div>';
            }).join('');
        }).catch(function () { target.textContent = ''; });
    }

    form.addEventListener('input', function () {
        clearTimeout(timer);
        timer = setTimeout(refresh, 500);
    });
    refresh();
})();
