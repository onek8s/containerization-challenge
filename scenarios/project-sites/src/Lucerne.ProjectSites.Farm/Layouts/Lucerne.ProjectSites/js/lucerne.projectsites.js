/* Lucerne Project Sites - client helpers for project sites (JSOM + jQuery).
   Loaded on every page of a project site by a ScriptLink custom action. */
var Lucerne = Lucerne || {};
Lucerne.ProjectSites = (function ($) {
    'use strict';

    var numberPattern = /^P\d{5}-[A-Z]{3}-[A-Z]{3}-[A-Z]{2,4}-\d{4}$/;

    function selectedItems() {
        var ctx = SP.ClientContext.get_current();
        return SP.ListOperation.Selection.getSelectedItems(ctx) || [];
    }

    function hasSelection() {
        return selectedItems().length > 0;
    }

    function notify(text, sticky) {
        return SP.UI.Notify.addNotification(text, sticky === true);
    }

    /* Sets LucApprovalStatus on the selected documents. Approvals also record approver and date;
       the event receiver checks the revision rules, the sync service submits approved documents. */
    function setApproval(status) {
        var ctx = SP.ClientContext.get_current();
        var listId = SP.ListOperation.Selection.getSelectedList();
        var list = ctx.get_web().get_lists().getById(listId);
        var user = ctx.get_web().get_currentUser();
        var items = selectedItems();
        var waiting = notify('Updating ' + items.length + ' document(s)...', true);

        $.each(items, function (i, sel) {
            var item = list.getItemById(sel.id);
            item.set_item('LucApprovalStatus', status);
            if (status === 'Approved') {
                item.set_item('LucApprovedBy', user);
                item.set_item('LucApprovedDate', new Date());
                item.set_item('LucDeliverableStatus', 'Approved');
            } else if (status === 'Pending') {
                item.set_item('LucDeliverableStatus', 'ForReview');
            }
            item.update();
        });

        ctx.executeQueryAsync(function () {
            SP.UI.Notify.removeNotification(waiting);
            notify('Documents updated.');
            SP.UI.ModalDialog.RefreshPage(SP.UI.DialogResult.OK);
        }, function (sender, args) {
            SP.UI.Notify.removeNotification(waiting);
            notify('Update failed: ' + args.get_message(), true);
        });
    }

    /* Highlights document numbers that do not follow the numbering standard in list views. */
    function checkNumbers() {
        $('td.ms-vb2, td.ms-cellstyle').each(function () {
            var text = $.trim($(this).text());
            if (/^P\d{5}-/.test(text) && !numberPattern.test(text)) {
                $(this).addClass('luc-badnumber').attr('title', 'Number does not follow LE-QP-07');
            }
        });
    }

    /* Shows the project status banner on the site home page (reads the hub via REST, cached for the session). */
    function statusBanner() {
        var $banner = $('#luc-status-banner');
        if (!$banner.length || !window._spPageContextInfo) {
            return;
        }
        var hub = $banner.data('hub');
        var number = $banner.data('project');
        var cacheKey = 'luc-status-' + number;
        var cached = window.sessionStorage && sessionStorage.getItem(cacheKey);
        if (cached) {
            render(JSON.parse(cached));
            return;
        }
        $.ajax({
            url: hub + "/_api/web/lists/getbytitle('Project Status')/items?$select=LucOverallStatus,LucStatusSummary,LucAssessedDate&$filter=LucProjectNumber eq '" + number + "'",
            headers: { Accept: 'application/json;odata=verbose' }
        }).done(function (data) {
            var row = data.d.results[0];
            if (row) {
                if (window.sessionStorage) {
                    sessionStorage.setItem(cacheKey, JSON.stringify(row));
                }
                render(row);
            }
        });

        function render(row) {
            $banner.addClass('luc-rag-' + (row.LucOverallStatus || 'notassessed').toLowerCase())
                .text(row.LucOverallStatus + ': ' + (row.LucStatusSummary || ''));
        }
    }

    $(function () {
        SP.SOD.executeFunc('sp.js', 'SP.ClientContext', function () {
            checkNumbers();
            statusBanner();
        });
    });

    return {
        hasSelection: hasSelection,
        setApproval: setApproval,
        checkNumbers: checkNumbers
    };
}(jQuery));
