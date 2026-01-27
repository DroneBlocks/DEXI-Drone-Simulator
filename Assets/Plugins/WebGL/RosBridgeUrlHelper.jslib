mergeInto(LibraryManager.library, {

    // Get rosbridge URL from query parameter (e.g., ?rosbridge=ws://example.com:9090)
    GetRosBridgeUrlFromQuery: function() {
        var urlParams = new URLSearchParams(window.location.search);
        var rosbridgeUrl = urlParams.get('rosbridge');

        if (rosbridgeUrl) {
            var bufferSize = lengthBytesUTF8(rosbridgeUrl) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(rosbridgeUrl, buffer, bufferSize);
            return buffer;
        }

        return null;
    },

    // Get namespace from query parameter (e.g., ?namespace=dexi1)
    // Returns empty string if not provided (for backward compatibility)
    GetNamespaceFromQuery: function() {
        var urlParams = new URLSearchParams(window.location.search);
        var namespace = urlParams.get('namespace');

        // Return empty string if no namespace (not null, for easier C# handling)
        var result = namespace || '';
        var bufferSize = lengthBytesUTF8(result) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(result, buffer, bufferSize);
        return buffer;
    },

    // Get hostname from current page
    GetHostname: function() {
        var hostname = window.location.hostname;
        var bufferSize = lengthBytesUTF8(hostname) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(hostname, buffer, bufferSize);
        return buffer;
    },

    // Get full origin (protocol + hostname + port)
    GetOrigin: function() {
        var origin = window.location.origin;
        var bufferSize = lengthBytesUTF8(origin) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(origin, buffer, bufferSize);
        return buffer;
    },

    // Check if page is served over HTTPS
    IsSecureContext: function() {
        return window.location.protocol === 'https:' ? 1 : 0;
    }

});
