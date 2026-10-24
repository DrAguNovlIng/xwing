window.xwingTheme = {
    setPreference: function (value, maxAgeSeconds) {
        const encoded = encodeURIComponent(value);
        document.cookie = 'xwing-theme=' + encoded + '; path=/; max-age=' + maxAgeSeconds + '; SameSite=Lax';
    },
    capturePointer: function (element, pointerId) {
        if (element && typeof element.setPointerCapture === 'function') {
            element.setPointerCapture(pointerId);
        }
    }
};
