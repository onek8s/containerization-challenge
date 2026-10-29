/* Live order status from the OrderStatusHub (SignalR 2). Updates labels on the page and shows a short note. */
(function ($) {
    if ($('body').data('signed-in') !== true || !$.connection || !$.connection.OrderStatusHub) return;

    var hub = $.connection.OrderStatusHub;
    hub.client.statusChanged = function (push) {
        var note = $('<div class="alert alert-info alert-dismissible live"></div>')
            .text((push.number ? push.number + ': ' : '') + (push.text || push.status));
        note.prepend('<button type="button" class="close" data-dismiss="alert">&times;</button>');
        $('#live-status').prepend(note);
        setTimeout(function () { note.fadeOut(600, function () { note.remove(); }); }, 15000);
        if (push.orderId) {
            $('[data-order-id="' + push.orderId + '"] .order-status').text(push.status).addClass('updated');
        }
    };

    function start() {
        $.connection.hub.start().fail(function () { setTimeout(start, 30000); });
    }
    $.connection.hub.disconnected(function () { setTimeout(start, 10000); });
    start();
})(jQuery);
