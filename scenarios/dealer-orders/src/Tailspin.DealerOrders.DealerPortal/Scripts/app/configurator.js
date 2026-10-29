/* Machine configurator: loads the option groups, sends every change to the server for rule checking and pricing. */
(function ($, ko) {
    var root = $('#configurator');
    if (root.length === 0) return;

    function ConfiguratorViewModel() {
        var self = this;
        self.modelCode = root.data('model');
        self.quoteId = root.data('quote-id') || null;
        self.currency = ko.observable('');
        self.groups = ko.observableArray([]);
        self.selected = ko.observableArray([]);
        self.result = ko.observable(null);
        self.quantity = ko.observable(1);
        self.extraDiscount = ko.observable(0);
        self.endCustomer = ko.observable('');
        self.reference = ko.observable('');
        self.saveError = ko.observable('');
        self.busy = ko.observable(false);

        self.money = function (value) { return tailspin.money(value, self.currency()); };
        self.isSelected = function (code) { return self.selected.indexOf(code) >= 0; };
        self.isAdded = function (code) { var r = self.result(); return r && r.addedByRules.indexOf(code) >= 0; };

        self.toggle = function (group, code) {
            if (group.kind === 'Single') {
                group.options.forEach(function (o) { self.selected.remove(o.code); });
                self.selected.push(code);
            } else if (self.isSelected(code)) {
                self.selected.remove(code);
            } else {
                self.selected.push(code);
            }
            self.evaluate();
        };

        var pending = null;
        self.evaluate = function () {
            if (pending) pending.abort();
            pending = tailspin.postJson(root.data('evaluate-url'), {
                modelCode: self.modelCode,
                options: self.selected(),
                quantity: Number(self.quantity()) || 1,
                extraDiscount: Number(self.extraDiscount()) || 0
            }).done(function (r) {
                self.result(r);
                // The server adds options required by the rules; keep the page in step.
                self.selected(r.options.slice(0));
            }).always(function () { pending = null; });
        };

        self.quantity.subscribe(self.evaluate);
        self.extraDiscount.subscribe(self.evaluate);

        self.canSave = ko.pureComputed(function () {
            var r = self.result();
            return r && r.isValid && !self.busy();
        });

        self.save = function () {
            self.busy(true);
            self.saveError('');
            tailspin.postJson(root.data('save-url'), {
                modelCode: self.modelCode,
                options: self.selected(),
                quantity: Number(self.quantity()) || 1,
                extraDiscount: Number(self.extraDiscount()) || 0,
                endCustomer: self.endCustomer(),
                reference: self.reference(),
                addToQuoteId: self.quoteId
            }).done(function (r) {
                window.location = r.url;
            }).fail(function (xhr) {
                self.saveError(tailspin.errorText(xhr));
                self.busy(false);
            });
        };

        tailspin.getJson(root.data('model-url')).done(function (model) {
            self.currency(model.currency);
            self.groups(model.groups);
            self.selected(model.defaultOptions);
            self.evaluate();
        });
    }

    ko.applyBindings(new ConfiguratorViewModel(), root[0]);
})(jQuery, ko);
