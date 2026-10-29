/* Customer Care: live updates through the queue hub (SignalR) and small page helpers. */
var care = (function ($) {
    'use strict';

    var handlers = { caseChanged: [], statsChanged: [], viewersChanged: [], caseUpdated: [] };
    var hub = $.connection ? $.connection.queueHub : null;

    function fire(name, args) {
        $.each(handlers[name], function (_, fn) { fn.apply(null, args); });
    }

    if (hub) {
        hub.client.caseChanged = function () { fire('caseChanged', arguments); };
        hub.client.statsChanged = function () { fire('statsChanged', arguments); };
        hub.client.viewersChanged = function () { fire('viewersChanged', arguments); };
        hub.client.caseUpdated = function () { fire('caseUpdated', arguments); };
    }

    function connect(onStarted) {
        if (!hub) { return; }
        $.connection.hub.start().done(function () { if (onStarted) { onStarted(hub); } });
        $.connection.hub.disconnected(function () {
            // Server restarted or the app pool recycled: try again in a while.
            setTimeout(function () { connect(onStarted); }, 10000);
        });
    }

    function showViewers(viewers) {
        var $v = $('#viewers');
        var me = $('.top .user').text();
        var others = $.grep(viewers || [], function (n) { return n !== me; });
        if (others.length === 0) { $v.addClass('hidden'); return; }
        $v.text('Also viewing this case: ' + others.join(', ')).removeClass('hidden');
    }

    var reloading = false;
    function reloadBoard($board) {
        if (reloading) { return; }
        reloading = true;
        $.getJSON($board.data('url')).done(function (columns) {
            $.each(columns, function (i, col) {
                var $col = $board.find('.board-column').eq(i);
                $col.find('.count').text(col.Cases.length);
                $col.find('a.card').remove();
                $.each(col.Cases, function (_, c) {
                    var $a = $('<a class="card"/>')
                        .addClass('sla-' + c.Sla.toLowerCase())
                        .addClass('priority-' + c.Priority.toLowerCase())
                        .attr('href', $('.brand').attr('href') + 'cases/' + c.Id);
                    $('<span class="card-number"/>').text(c.CaseNumber).appendTo($a);
                    $('<span class="card-subject"/>').text(c.Subject).appendTo($a);
                    $('<span class="card-meta"/>').text(c.Priority + ' · due ' + c.ResolveDue + (c.AssignedTo ? ' · ' + c.AssignedTo.replace(/^.*\\/, '') : '')).appendTo($a);
                    $col.append($a);
                });
            });
        }).always(function () { reloading = false; });
    }

    return {
        connect: connect,
        onCaseChanged: function (fn) { handlers.caseChanged.push(fn); },
        onStatsChanged: function (fn) { handlers.statsChanged.push(fn); },
        onViewersChanged: function (fn) { handlers.viewersChanged.push(fn); },
        onCaseUpdated: function (fn) { handlers.caseUpdated.push(fn); },
        showViewers: showViewers,
        reloadBoard: reloadBoard
    };
})(jQuery);
