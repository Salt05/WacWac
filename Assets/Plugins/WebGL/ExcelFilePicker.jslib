mergeInto(LibraryManager.library, {

    // Opens a native file-picker dialog (xlsx or csv).
    // On success: calls callbackMethod on goName with "csv:<text>" or "xlsx:<base64>".
    // On failure: calls errMethod on goName with an error message string.
    JS_OpenFilePicker: function (goNamePtr, cbMethodPtr, errMethodPtr) {
        var goName    = UTF8ToString(goNamePtr);
        var cbMethod  = UTF8ToString(cbMethodPtr);
        var errMethod = UTF8ToString(errMethodPtr);

        var input = document.createElement('input');
        input.type   = 'file';
        input.accept = '.xlsx,.csv';
        input.style.cssText = 'position:fixed;top:-9999px;left:-9999px;opacity:0;';
        document.body.appendChild(input);

        input.addEventListener('change', function () {
            var file = input.files && input.files[0];
            document.body.removeChild(input);
            if (!file) return;

            var isXlsx = file.name.toLowerCase().endsWith('.xlsx');

            if (!isXlsx) {
                // ---- CSV: read as UTF-8 text ----
                var reader = new FileReader();
                reader.onload = function (e) {
                    SendMessage(goName, cbMethod, 'csv:' + e.target.result);
                };
                reader.onerror = function () {
                    SendMessage(goName, errMethod, 'Cannot read CSV file');
                };
                reader.readAsText(file, 'UTF-8');

            } else {
                // ---- XLSX: read as bytes, encode to base64 ----
                var reader = new FileReader();
                reader.onload = function (e) {
                    try {
                        var bytes     = new Uint8Array(e.target.result);
                        var chunkSize = 0x8000; // 32 KB chunks to avoid call-stack overflow
                        var binary    = '';
                        for (var i = 0; i < bytes.length; i += chunkSize) {
                            binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunkSize));
                        }
                        SendMessage(goName, cbMethod, 'xlsx:' + btoa(binary));
                    } catch (ex) {
                        SendMessage(goName, errMethod, 'Encode failed: ' + ex.message);
                    }
                };
                reader.onerror = function () {
                    SendMessage(goName, errMethod, 'Cannot read XLSX file');
                };
                reader.readAsArrayBuffer(file);
            }
        });

        input.click();
    }
});
