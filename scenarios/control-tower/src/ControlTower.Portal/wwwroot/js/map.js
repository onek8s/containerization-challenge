(function () {
    'use strict';
    var el = document.getElementById('map');
    if (!el || typeof L === 'undefined') { return; }
    var truck = [parseFloat(el.dataset.lat), parseFloat(el.dataset.lon)];
    var dest = [parseFloat(el.dataset.destLat), parseFloat(el.dataset.destLon)];
    var map = L.map(el);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 18,
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);
    L.circleMarker(truck, { radius: 8, color: '#1f3a5f', fillOpacity: 0.9 }).addTo(map).bindTooltip('Your goods');
    L.marker(dest).addTo(map).bindTooltip('Delivery address');
    map.fitBounds([truck, dest], { padding: [30, 30], maxZoom: 11 });
})();
