// Fabrikam Housing back office
var housing = housing || {};

housing.propertyMap = function (id) {
    var el = document.getElementById(id);
    if (!el || typeof L === 'undefined') return;
    var lat = parseFloat(el.getAttribute('data-lat'));
    var lon = parseFloat(el.getAttribute('data-lon'));
    var map = L.map(el).setView([lat, lon], 16);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);
    L.marker([lat, lon]).addTo(map).bindPopup(el.getAttribute('data-label'));
};

$(function () {
    // Stop double submits on slow forms (PDF generation, document uploads).
    $('form[method="post"]').on('submit', function () {
        var $btn = $(this).find('button[type="submit"], button:not([type])');
        setTimeout(function () { $btn.prop('disabled', true); }, 0);
    });
});
