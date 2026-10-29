/* Lucerne Project Sites - hub helpers: site request form defaults and project register filter. */
var Lucerne = Lucerne || {};
Lucerne.Hub = (function ($) {
    'use strict';

    function prefillRequestForm() {
        // NewForm.aspx of Site Requests: ?project=P19041 fills the number field
        var match = /[?&]project=(P\d{5})/i.exec(window.location.search);
        if (match && /SiteRequests\/NewForm\.aspx/i.test(window.location.pathname)) {
            $("input[title^='Project Number']").val(match[1].toUpperCase());
        }
    }

    function officeFilter() {
        var $select = $('#luc-office-filter');
        if (!$select.length) {
            return;
        }
        $select.on('change', function () {
            var office = $(this).val();
            $('table.luc-health tr').each(function (i) {
                if (i === 0) { return; }
                var cell = $(this).children('td').eq(1).text();
                $(this).toggle(!office || cell === office);
            });
        });
    }

    $(function () {
        prefillRequestForm();
        officeFilter();
    });

    return { prefillRequestForm: prefillRequestForm };
}(jQuery));
