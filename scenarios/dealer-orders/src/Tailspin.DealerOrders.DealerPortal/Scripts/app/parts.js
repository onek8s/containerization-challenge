/* Parts quick order: each line is looked up for description, price and stock as it is typed. */
(function ($, ko) {
    var root = $('#parts-order');
    if (root.length === 0) return;

    function Line() {
        this.itemId = ko.observable('');
        this.quantity = ko.observable(1);
        this.reference = ko.observable('');
        this.description = ko.observable('');
        this.netPrice = ko.observable(0);
        this.stockText = ko.observable('');
        this.notFound = ko.observable(false);
    }

    function PartsViewModel() {
        var self = this;
        self.lines = ko.observableArray([new Line(), new Line(), new Line()]);
        self.currency = ko.observable('');
        self.dealerPoNumber = ko.observable('');
        self.requestedDelivery = ko.observable('');
        self.deliveryAddressId = ko.observable();
        self.notes = ko.observable('');
        self.error = ko.observable('');
        self.busy = ko.observable(false);

        self.money = function (value) { return tailspin.money(value); };
        self.add = function () { self.lines.push(new Line()); };
        self.remove = function (line) { self.lines.remove(line); };

        self.lookup = function (line) {
            if (!line.itemId()) return;
            tailspin.getJson(root.data('lookup-url'), { itemId: line.itemId(), quantity: line.quantity() }).done(function (r) {
                line.notFound(!r.found);
                if (!r.found) { line.description(''); line.netPrice(0); line.stockText(''); return; }
                self.currency(r.currency);
                line.itemId(r.itemId);
                line.description(r.description);
                line.netPrice(r.netPrice);
                line.stockText(r.available >= line.quantity() ? 'in stock' : (r.available > 0 ? r.available + ' available' : (r.nextReceipt ? 'from ' + r.nextReceipt : 'on order')));
            });
        };

        self.total = ko.pureComputed(function () {
            return self.lines().reduce(function (sum, l) { return sum + l.netPrice() * l.quantity(); }, 0);
        });

        self.canPlace = ko.pureComputed(function () {
            return !self.busy() && self.lines().some(function (l) { return l.itemId() && !l.notFound(); });
        });

        self.place = function () {
            self.busy(true);
            self.error('');
            tailspin.postJson(root.data('place-url'), {
                dealerPoNumber: self.dealerPoNumber(),
                requestedDelivery: self.requestedDelivery() || null,
                deliveryAddressId: self.deliveryAddressId(),
                notes: self.notes(),
                lines: self.lines().filter(function (l) { return l.itemId(); }).map(function (l) {
                    return { itemId: l.itemId(), quantity: Number(l.quantity()), reference: l.reference() };
                })
            }).done(function (r) {
                window.location = r.url;
            }).fail(function (xhr) {
                self.error(tailspin.errorText(xhr));
                self.busy(false);
            });
        };
    }

    ko.applyBindings(new PartsViewModel(), root[0]);
})(jQuery, ko);
