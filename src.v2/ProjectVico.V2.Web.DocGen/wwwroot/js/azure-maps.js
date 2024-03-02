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

    // Add a click event to the map.
    map.events.add('click', map, function (e) {
        // Get the position that was clicked.
        var position = e.position;

        // Convert the position to a point.
        var point = new atlas.data.Point(position);

        // Create a data source and add it to the map.
        var dataSource = new atlas.source.DataSource();
        map.sources.add(dataSource);

        // Add the point to the data source.
        dataSource.add(new atlas.data.Feature(point));

        // Update the form with the latitude and longitude.
        DotNet.invokeMethodAsync('YourBlazorApp', 'UpdateForm', position.latitude, position.longitude);
    });
}
