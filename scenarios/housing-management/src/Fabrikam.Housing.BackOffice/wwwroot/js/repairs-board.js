// Live repairs board: loads open work orders, then applies changes pushed by the RepairsHub.
(function () {
    var board = document.getElementById('board');
    if (!board) return;
    var patch = board.getAttribute('data-patch');
    var cards = {};

    function cardHtml(w) {
        return '<a href="/Repairs/Details/' + w.id + '" class="text-reset text-decoration-none"><strong>' + w.reference + '</strong> ' +
            (w.contractor || '') + '<br />' + escapeHtml(w.address) + '<br /><span class="text-muted">' + (w.target || w.at || '') + '</span></a>';
    }

    function escapeHtml(s) {
        return (s || '').replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; });
    }

    function place(w, flash) {
        var el = cards[w.id];
        if (!el) {
            el = document.createElement('div');
            cards[w.id] = el;
        }
        el.className = 'board-card ' + w.priority.toLowerCase() + (w.overdue ? ' overdue' : '') + (flash ? ' flash' : '');
        el.innerHTML = cardHtml(w);
        var column = board.querySelector('.board-cards[data-status="' + w.status + '"]');
        if (column) {
            column.appendChild(el);
        } else if (el.parentNode) {
            el.parentNode.removeChild(el);
            delete cards[w.id];
        }
        updateCounts();
    }

    function updateCounts() {
        board.querySelectorAll('.board-cards').forEach(function (c) {
            var badge = board.querySelector('[data-count="' + c.getAttribute('data-status') + '"]');
            if (badge) badge.textContent = c.children.length;
        });
    }

    fetch(board.getAttribute('data-source'), { credentials: 'same-origin' })
        .then(function (r) { return r.json(); })
        .then(function (items) { items.forEach(function (w) { place(w, false); }); });

    var status = document.getElementById('board-connection');
    var connection = new signalR.HubConnectionBuilder()
        .withUrl(board.getAttribute('data-hub'))
        .withAutomaticReconnect()
        .build();

    connection.on('workOrderChanged', function (u) {
        place({ id: u.workOrderId, reference: u.reference, address: u.address, priority: u.priority, status: u.status, contractor: u.contractor, at: u.at, overdue: u.overdue }, true);
        var li = document.createElement('li');
        li.textContent = u.at + ' ' + u.reference + ' ' + u.status + ' (' + (u.actor || '') + ') ' + (u.text || '');
        var log = document.getElementById('board-activity');
        log.insertBefore(li, log.firstChild);
        while (log.children.length > 30) log.removeChild(log.lastChild);
    });
    connection.onreconnecting(function () { status.className = 'badge badge-warning'; status.textContent = 'reconnecting'; });
    connection.onreconnected(function () { status.className = 'badge badge-success'; status.textContent = 'live'; connection.invoke('JoinPatch', patch); });

    connection.start().then(function () {
        status.className = 'badge badge-success';
        status.textContent = 'live';
        return connection.invoke('JoinPatch', patch);
    }).catch(function (err) {
        status.className = 'badge badge-danger';
        status.textContent = 'offline';
        console.error(err);
    });
})();
