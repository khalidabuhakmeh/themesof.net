(function() {
    window.initializeCompletionDialogInput = function(inputRef, listRef, componentRef) {
        inputRef.focus();
        inputRef.onkeydown = function(e) {
            if (e.key == "ArrowUp") {
                componentRef.invokeMethodAsync("SelectPreviousElement");
                e.preventDefault();
            }
            else if (e.key == "ArrowDown") {
                componentRef.invokeMethodAsync("SelectNextElement");
                e.preventDefault();
            }
        }

        let selectedElements = listRef.getElementsByClassName("selected");
        if (selectedElements.length > 0) {
            selectedElements[0].scrollIntoView({
                behavior: 'auto',
                block: 'nearest'
            });
        }
    }
    window.blurElement = function(inputRef) {
        inputRef.blur();
    }
})();

