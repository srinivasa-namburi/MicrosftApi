function initializeMap(subscriptionKey, initLat, initLong) {
    var map = new atlas.Map('map', {
        center: [parseFloat(initLong), parseFloat(initLat)],
        zoom: 12,
        view: 'Auto',
        authOptions: {
            authType: 'subscriptionKey',
            subscriptionKey: subscriptionKey
        }
    });

    //Wait until the map resources are ready.
    map.events.add('ready', function () {

        // adding marker logic
        const marker = new atlas.HtmlMarker({
            position: [initLong, initLat],
            draggable: true,
            // htmlContent: '<circle cx="10" cy="10" r="5" fill="blue" />' // Customize the pin appearance
        });

        // expose initial latitude and longitude
        var latitude = document.getElementById('latitude');
        var longitude = document.getElementById('longitude');

        // get marker position
        var pos = marker.getOptions().position;

        // Round longitude,latitude values to 5 decimal places.
        latitude.innerText = Math.round(pos[1] * 100000) / 100000;
        longitude.innerText = Math.round(pos[0] * 100000) / 100000;

        // Trigger the 'change' event to update Blazor state
        const changeEvent = new Event('change');
        // arbitrarily attached to "coordinateBox"
        var coordinateBox = document.getElementById('coordinateBox');
        coordinateBox.dispatchEvent(changeEvent);
        
        // expose latitude and longitude on drag events
        // add a drag event to get the position of the marker. Markers support drag, dragstart and dragend events.
        map.events.add('drag', marker, function () {
            var pos = marker.getOptions().position;

            // Round longitude,latitude values to 5 decimal places.
            latitude.innerText = Math.round(pos[1] * 100000) / 100000;
            longitude.innerText = Math.round(pos[0] * 100000) / 100000;
        });

        // when the user drops the marker, trigger another 'change' event to update Blazor state
        map.events.add('dragend', marker, function () {
            coordinateBox.dispatchEvent(changeEvent);
        });

        map.markers.add(marker);
    });
}
getInnerText = function (elementId) {
    const element = document.getElementById(elementId);
    return element ? element.innerText : "";
};
