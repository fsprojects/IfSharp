/**
 * This class provides intellisense for either a textarea or an inputbox.
 * Triggers can be added 
 * 
 * @param {string|HTMLElement} editor - The id of a textarea or inputbox or the actual element
 * @class CodeMirrorIntellisense
 */
var CodeMirrorIntellisense = function (editor)
{
    var decls = new DeclarationsIntellisense();
    var meths = new MethodsIntellisense();
    var triggers = { upDecls: [], downDecls: [], upMeths: [], downMeths: [] };
    var declarationsCallback = null;
    var methodsCallback = null;
    var autoCompleteStart = { lineIndex: 0, columnIndex: 0 };
    var triggered = false;

    function processTriggers(triggers, evt, callback)
    {
        for (var k in triggers)
        {
            var item = triggers[k];
            var shiftKey = item.shiftKey || false;
            var ctrlKey = item.ctrlKey || false;
            var keyCode = item.keyCode || 0;
            var preventDefault = item.preventDefault || false;

            if (evt.keyCode === keyCode && evt.shiftKey === shiftKey && evt.ctrlKey === ctrlKey)
            {
                var cursor = editor.getCursor();
                autoCompleteStart.columnIndex = cursor.ch;
                autoCompleteStart.lineIndex = cursor.line;
                callback(item);
                if (preventDefault)
                {
                    evt.preventDefault();
                    evt.cancelBubble = true;
                }
                return true;
            }
        }
        return false;
    }

    function setEditor(e)
    {
        editor = e;
        editor.on('keyup', function (cm, evt)
        {
            if (decls.isVisible())
            {
                decls.setFilter(getFilterText());
            }

            if (!processTriggers(triggers.upDecls, evt, declarationsCallback))
            {
                processTriggers(triggers.upMeths, evt, methodsCallback);
            }
        });

        editor.on('keydown', function (cm, evt)
        {
            if (decls.isVisible())
            {
                if (evt.keyCode === 8)
                {
                    decls.setFilter(getFilterText());
                }
                else
                {
                    decls.handleKeyDown(evt);
                }
            }
            if (!processTriggers(triggers.downDecls, evt, declarationsCallback))
            {
                processTriggers(triggers.downMeths, evt, methodsCallback);
            }
            if (meths.isVisible())
            {
                meths.handleKeyDown(evt);
            }
        });
    }

    // when the visiblity has changed for the declarations, set the position of the methods UI
    decls.onVisibleChanged(function (v)
    {
        if (v)
        {
            var coords = editor.cursorCoords(true, 'page');
            decls.setPosition(coords.left, coords.bottom);
        }
    });

    // when the visiblity has changed for the methods, set the position of the methods UI
    meths.onVisibleChanged(function (v)
    {
        if (v)
        {
            var coords = editor.cursorCoords(true, 'page');
            meths.setPosition(coords.left, coords.bottom);
        }
    });

    // when an item is chosen by the declarations UI, set the value.
    decls.onItemChosen(function (item)
    {
        var itemValue = item.value || item.name;
        var cursor = editor.getCursor();
        var line = editor.getLine(autoCompleteStart.lineIndex);

        var startRange = { line: cursor.line, ch: autoCompleteStart.columnIndex };
        var endRange = { line: cursor.line, ch: cursor.ch };
        editor.replaceRange(itemValue, startRange, endRange);
        editor.setSelection({ line: cursor.line, ch: cursor.ch + itemValue.length });
        decls.setVisible(false);
        editor.focus();
    });

    function addTrigger(trigger, methsOrDecls)
    {
        var type = trigger.type || 'up';
        if (triggers[type + methsOrDecls])
        {
            triggers[type + methsOrDecls].push(trigger);
        }
    }

    function addDeclarationTrigger(trigger)
    {
        addTrigger(trigger, 'Decls');
    }

    function addMethodsTrigger(trigger)
    {
        addTrigger(trigger, 'Meths');
    }

    function onDeclaration(callback)
    {
        declarationsCallback = callback;
    }

    function onMethod(callback)
    {
        methodsCallback = callback;
    }

    function getFilterText()
    {
        var cursor = editor.getCursor();
        var line = editor.getLine(autoCompleteStart.lineIndex);
        return line.substring(autoCompleteStart.columnIndex, cursor.ch);
    }

    // set the editor
    setEditor(editor);

    /**
     * Gets the declarations user interface
     * @returns {DeclarationsIntellisense}
     * @function getDecls
     */
    this.getDecls = function () { return decls; };

    /**
     * Gets the methods user interface
     * @returns {MethodsIntellisense}
     * @function getMeths
     */
    this.getMeths = function () { return meths; };

    /**
     * Adds a trigger to the list of triggers that can cause the declarations user interface
     * to popup.
     * @instance
     * @param {KeyTrigger} trigger - The trigger to add
     * @function addDeclarationTrigger
     */
    this.addDeclarationTrigger = addDeclarationTrigger;

    /**
     * Adds a trigger to the list of triggers that can cause the methods user interface
     * to popup.
     * @param {KeyTrigger} trigger - The trigger to add
     * @function addMethodsTrigger
     */
    this.addMethodsTrigger = addMethodsTrigger;

    /**
     * Sets a callback to invoke when a key is pressed that causes the declarations list to
     * popup.
     * @param {function} callback - The callback to set
     * @function onDeclaration
     */
    this.onDeclaration = onDeclaration;

    /**
     * Sets a callback to invoke when a key is pressed that causes the methods list to
     * popup.
     * @param {function} callback - The callback to set
     * @function onMethod
     */
    this.onMethod = onMethod;

    /**
     * Delegate for setting the methods to display to the user
     * @param {string[]} data - The methods to display
     * @function setMethods
     */
    this.setMethods = function (data) { meths.setMethods(data); };

    /**
     * Delegate for setting the declarations to display to the user
     * @param {DeclarationItem[]} data - The declarations to display
     * @function setDeclarations
     */
    this.setDeclarations = function (data) { decls.setDeclarations(data); };

    /**
     * Sets the starting location where filtering can occur. This is set when
     * a trigger happens that would cause the declarations list to show
     * @param {int} i - The index to set
     * @function setStartColumnIndex
     */
    this.setStartColumnIndex = function (i) { autoCompleteStart.columnIndex = i; };

    /**
     * Gets the text after startColumnIndex but before caret offset.
     * @returns {int}
     * @function getFilterText
     */
    this.getFilterText = getFilterText;
};