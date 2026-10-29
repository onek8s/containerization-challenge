/* Shared helpers: JSON calls with the anti-forgery token, money formatting. */
var tailspin = window.tailspin || {};

tailspin.token = function () {
    return $('input[name="__RequestVerificationToken"]').first().val();
};

tailspin.postJson = function (url, data) {
    return $.ajax({
        url: url,
        type: 'POST',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        headers: { 'RequestVerificationToken': tailspin.token(), 'X-Requested-With': 'XMLHttpRequest' },
        data: JSON.stringify(data)
    });
};

tailspin.getJson = function (url, data) {
    return $.ajax({ url: url, type: 'GET', dataType: 'json', data: data, cache: false, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
};

tailspin.money = function (value, currency) {
    if (value === null || value === undefined || isNaN(value)) return '';
    var text = Number(value).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    return currency ? text + ' ' + currency : text;
};

tailspin.errorText = function (xhr) {
    if (xhr && xhr.responseJSON && xhr.responseJSON.error) return xhr.responseJSON.error;
    return 'The request failed (' + (xhr ? xhr.status : '?') + '). Try again.';
};
