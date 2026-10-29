// Live status and ETA updates for the shipments of the signed-in customer.
(function () {
    'use strict';
    if (typeof signalR === 'undefined') { return; }
    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/tracking')
        .withAutomaticReconnect([0, 2000, 10000, 30000, 60000])
        .build();

    function rows(id) { return document.querySelectorAll('[data-shipment="' + id + '"]'); }

    function flash(el) {
        el.classList.remove('flash');
        void el.offsetWidth;
        el.classList.add('flash');
    }

    connection.on('milestone', function (m) {
        rows(m.shipmentId).forEach(function (row) {
            var status = row.querySelector('.status');
            if (status) { status.textContent = m.status; }
            flash(row);
        });
        var timeline = document.getElementById('timeline');
        if (timeline && document.querySelector('[data-shipment="' + m.shipmentId + '"]')) {
            var li = document.createElement('li');
            var at = new Date(m.occurredAt);
            li.innerHTML = '<span class="text-muted"></span> <strong></strong> ';
            li.querySelector('span').textContent = at.toLocaleString();
            li.querySelector('strong').textContent = m.description;
            li.appendChild(document.createTextNode(m.location || ''));
            timeline.insertBefore(li, timeline.firstChild);
            flash(li);
        }
    });

    connection.on('eta', function (e) {
        rows(e.shipmentId).forEach(function (row) {
            var cell = row.querySelector('.eta') || document.querySelector('.eta');
            if (cell) { cell.textContent = new Date(e.eta).toLocaleString(); flash(cell); }
        });
    });

    connection.start().catch(function (err) { console.warn('Live updates unavailable', err); });
})();
