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
    },

    // Get a URL query parameter as integer, returns defaultValue if not found
    GetIntParam: function(namePtr, defaultValue) {
        var name = UTF8ToString(namePtr);
        var urlParams = new URLSearchParams(window.location.search);
        var val = urlParams.get(name);
        if (val !== null) {
            var parsed = parseInt(val, 10);
            return isNaN(parsed) ? defaultValue : parsed;
        }
        return defaultValue;
    },

    // Get the number of connected gamepads (browser Gamepad API)
    GetGamepadCount: function() {
        var gamepads = navigator.getGamepads();
        var count = 0;
        for (var i = 0; i < gamepads.length; i++) {
            if (gamepads[i]) count++;
        }
        return count;
    },

    // Get a gamepad axis value directly from the browser Gamepad API
    // gamepadIndex: which gamepad (0-based, skipping nulls)
    // axisIndex: which axis
    // Returns 0 if not found
    GetGamepadAxis: function(gamepadIndex, axisIndex) {
        var gamepads = navigator.getGamepads();
        var found = 0;
        for (var i = 0; i < gamepads.length; i++) {
            if (gamepads[i]) {
                if (found === gamepadIndex) {
                    var gp = gamepads[i];
                    if (axisIndex >= 0 && axisIndex < gp.axes.length) {
                        return gp.axes[axisIndex];
                    }
                    return 0;
                }
                found++;
            }
        }
        return 0;
    },

    // Get a gamepad button state (1=pressed, 0=not pressed)
    GetGamepadButton: function(gamepadIndex, buttonIndex) {
        var gamepads = navigator.getGamepads();
        var found = 0;
        for (var i = 0; i < gamepads.length; i++) {
            if (gamepads[i]) {
                if (found === gamepadIndex) {
                    var gp = gamepads[i];
                    if (buttonIndex >= 0 && buttonIndex < gp.buttons.length) {
                        return gp.buttons[buttonIndex].pressed ? 1 : 0;
                    }
                    return 0;
                }
                found++;
            }
        }
        return 0;
    },

    // Get the number of axes on a gamepad
    GetGamepadAxisCount: function(gamepadIndex) {
        var gamepads = navigator.getGamepads();
        var found = 0;
        for (var i = 0; i < gamepads.length; i++) {
            if (gamepads[i]) {
                if (found === gamepadIndex) {
                    return gamepads[i].axes.length;
                }
                found++;
            }
        }
        return 0;
    },

    // Get gamepad name
    GetGamepadName: function(gamepadIndex) {
        var gamepads = navigator.getGamepads();
        var found = 0;
        for (var i = 0; i < gamepads.length; i++) {
            if (gamepads[i]) {
                if (found === gamepadIndex) {
                    var name = gamepads[i].id;
                    var bufferSize = lengthBytesUTF8(name) + 1;
                    var buffer = _malloc(bufferSize);
                    stringToUTF8(name, buffer, bufferSize);
                    return buffer;
                }
                found++;
            }
        }
        var empty = '';
        var bufferSize = 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(empty, buffer, bufferSize);
        return buffer;
    }

});
