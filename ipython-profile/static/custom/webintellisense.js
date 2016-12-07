/**
 * An item that is displayed within the declarations user interface.
 * @class DeclarationItem
 */
function DeclarationItem(name, value, glyph, documentation)
{
    /** 
     * The name displayed in the user interface
     * @type {string} 
     * @property name
     */
    this.name = name;

    /**
     * The value that is replaced when a declaration is selected by the user
     * @type {string}
     * @property value
     */
    this.value = value;

    /**
     * A number that represents what image to display for this item. The css class
     * for the user interface item is `icon-glyph-{glyph}`. For example: `icon-glyph-1`.
     * 
     * An example CSS selector:
     * 
     *   .icon-glyph-1 {
     *       background-image: url('css/folder.png');
     *   }
     * 
     * @type {int}
     * @property glyph
     */
    this.glyph = glyph;

    /** 
     * A piece of documentation to display when this item is selected by the user.
     * @type {string}
     * @property documentation
     */
    this.documentation = documentation;
}

/**
 * Provides some utility methods.
 * @class Utils
 */
var Utils = function ()
{
    function showElement(el, b)
    {
        el.style.display = b ? 'block' : 'none';
    }

    function hasCssClass(el, name)
    {
        var classes = el.className.split(/\s+/g);
        return classes.indexOf(name) !== -1;
    }

    function addCssClass(el, name)
    {
        if (!hasCssClass(el, name))
        {
            el.className += " " + name;
        }
    }

    function removeCssClass(el, name)
    {
        var classes = el.className.split(/\s+/g);
        while (true)
        {
            var index = classes.indexOf(name);
            if (index === -1)
            {
                break;
            }
            classes.splice(index, 1);
        }
        el.className = classes.join(" ");
    }

    function lastIndexOfAny(str, arr, start)
    {
        var max = -1;
        for (var i = 0; i < arr.length; i++)
        {
            var val = str.lastIndexOf(arr[i], start);
            max = Math.max(max, val);
        }
        return max;
    }

    /**
     * Looks for the last index of a number of strings inside of another string
     * @param {string} str - The string to search within
     * @param {string[]} arr - An array of strings to search for
     * @param {number} [start] - Optional starting position
     * @function lastIndexOfAny
     */
    this.lastIndexOfAny = lastIndexOfAny;

    /**
     * Removes a CSS class from an element
     * @param {HTMLElement} element - The element to remove the class
     * @param {string} name - The name of the class to remove
     * @function removeCssClass
     */
    this.removeCssClass = removeCssClass;

    /**
     * Adds a CSS class from an element
     * @param {HTMLElement} element - The element to add the class
     * @param {string} name - The name of the class to add
     * @function addCssClass
     */
    this.addCssClass = addCssClass;

    /**
     * Checks to see if an element has a CSS class
     * @param {HTMLElement} element - The element to add the class
     * @returns {boolean}
     * @function hasCssClass
     */
    this.hasCssClass = hasCssClass;

    /**
     * Shows or hides an element by setting the display style to 'block' for true
     * or 'none' for false.
     * @param {HTMLElement} element - The element to show or hide
     * @param {boolean} b
     * @function showElement
     */
    this.showElement = showElement;
};

/**
 * Provides a user interface for a tooltip.
 * @class Tooltip
 */
var Tooltip = function ()
{
    var visible = false;
    var events = { visibleChanged: [] };
    var utils = new Utils();

    var tooltipElement = document.getElementById('br-tooltip-div');
    if (tooltipElement == null)
    {
        tooltipElement = document.createElement('div');
        tooltipElement.id = 'br-tooltip-div';
        tooltipElement.className = 'br-tooltip';
        document.body.appendChild(tooltipElement);
    }

    function triggerVisibleChanged()
    {
        events.visibleChanged.forEach(function (callback)
        {
            callback(visible);
        });
    }

    function setVisible(b)
    {
        if (visible !== b)
        {
            visible = b;
            utils.showElement(tooltipElement, b);
            triggerVisibleChanged();
        }
    }

    function setHtml(html)
    {
        tooltipElement.innerHTML = html;
    }

    function setText(text)
    {
        tooltipElement.innerText = text;
    }

    function getText()
    {
        return tooltipElement.innerText;
    }

    function getHtml()
    {
        return tooltipElement.innerHTML;
    }

    function setPosition(left, top)
    {
        tooltipElement.style.left = left + 'px';
        tooltipElement.style.top = top + 'px';
    }

    /**
     * Check to see if the user interface is visible or not
     * @function isVisible
     * @returns {bool} True if visible otherwise false
     */
    this.isVisible = function () { return visible; };

    /**
     * Sets the visibility of the tooltip element
     * @param {bool} b True to set visible, false to hide
     * @function setVisible
     */
    this.setVisible = setVisible;

    /**
     * Sets the text of the tooltip element
     * @param {string} text The text to set
     * @function setText
     */
    this.setText = setText;

    /**
     * Sets the HTML of the tooltip element
     * @param {string} html The html to set
     * @function setHtml
     */
    this.setHtml = setHtml;

    /**
     * Gets the inner text of the tooltip element
     * @function getText
     * @returns {string} The inner text of the element
     */
    this.getText = getText;

    /**
     * Gets the inner html of the tooltip elemnt
     * @function getHtml The inner html of the element
     */
    this.getHtml = getHtml;

    /**
     * Sets the position on screen of the tooltip element
     * @param {int} left The left pixel position
     * @param {int} top The top pixel position
     * @function setPosition
     */
    this.setPosition = setPosition;
};

/**
 * Provides a user interface for a methods popup. This class basically generates
 * a div that preview a list of strings.
 * 
 * @class MethodsIntellisense
 */
var MethodsIntellisense = function ()
{
    var utils = new Utils();
    var events = { visibleChanged: [] };
    var visible = false;
    var methods = [];
    var selectedIndex = 0;

    // methods
    var methodsElement = document.createElement('div');
    methodsElement.className = 'br-methods';

    // methods text
    var methodsTextElement = document.createElement('div');
    methodsTextElement.className = 'br-methods-text';

    // arrows
    var arrowsElement = document.createElement('div');
    arrowsElement.className = 'br-methods-arrows';

    // up arrow
    var upArrowElement = document.createElement('span');
    upArrowElement.className = 'br-methods-arrow';
    upArrowElement.innerHTML = '&#8593;';

    // down arrow
    var downArrowElement = document.createElement('span');
    downArrowElement.className = 'br-methods-arrow';
    downArrowElement.innerHTML = '&#8595;';

    // arrow text (1 of x)
    var arrowTextElement = document.createElement('span');
    arrowTextElement.className = 'br-methods-arrow-text';

    arrowsElement.appendChild(upArrowElement);
    arrowsElement.appendChild(arrowTextElement);
    arrowsElement.appendChild(downArrowElement);
    methodsElement.appendChild(arrowsElement);
    methodsElement.appendChild(methodsTextElement);
    document.body.appendChild(methodsElement);

    function setSelectedIndex(idx)
    {
        if (idx < 0)
        {
            idx = methods.length - 1;
        }
        else if (idx >= methods.length)
        {
            idx = 0;
        }

        selectedIndex = idx;
        methodsTextElement.innerHTML = methods[idx];
        arrowTextElement.innerHTML = (idx + 1) + ' of ' + methods.length;
    }

    function setMethods(data)
    {
        if (data != null && data.length > 0)
        {
            methods = data;

            // show the elements
            setVisible(true);

            // show the first item
            setSelectedIndex(0);
        }
    }

    function setPosition(left, top)
    {
        methodsElement.style.left = left + 'px';
        methodsElement.style.top = top + 'px';
    }

    function moveSelected(delta)
    {
        setSelectedIndex(selectedIndex + delta);
    }

    function isVisible()
    {
        return visible;
    }

    function setVisible(b)
    {
        if (visible !== b)
        {
            visible = b;
            utils.showElement(methodsElement, b);
            triggerVisibleChanged();
        }
    }

    function triggerVisibleChanged()
    {
        events.visibleChanged.forEach(function (callback)
        {
            callback(visible);
        });
    }

    function handleKeyDown(evt)
    {
        // escape, left, right
        if (evt.keyCode === 27 || evt.keyCode === 37 || evt.keyCode === 39)
        {
            setVisible(false);
        }
            // up
        else if (evt.keyCode === 38)
        {
            moveSelected(-1);
            evt.preventDefault();
            evt.stopPropagation();
        }
            // down
        else if (evt.keyCode === 40)
        {
            moveSelected(1);
            evt.preventDefault();
            evt.stopPropagation();
        }
            // page up 
        else if (evt.keyCode === 33)
        {
            moveSelected(-5);
            evt.preventDefault();
        }
            // page down
        else if (evt.keyCode === 34)
        {
            moveSelected(5);
            evt.preventDefault();
        }
    }

    function onVisibleChanged(callback)
    {
        events.visibleChanged.push(callback);
    }

    // arrow click events
    downArrowElement.onclick = function ()
    {
        moveSelected(1);
    };

    // arrow click events
    upArrowElement.onclick = function ()
    {
        moveSelected(-1);
    };

    /**
     * Shows or hides the UI
     * @function setVisible
     * @param {boolean} b
     */
    this.setVisible = setVisible;

    /**
     * Checks to see if the UI is visible
     * @function isVisible
     * @returns {boolean}
     */
    this.isVisible = isVisible;

    /**
     * Sets the selected item by index. Wrapping is performed if the index
     * specified is out of bounds of the methods that are set.
     * @function setSelectedIndex
     * @param {int} idx - The index of the item to set selected
     */
    this.setSelectedIndex = setSelectedIndex;

    /**
     * Sets the methods to display. If not empty, the user interface is shown and the
     * first methods is selected.
     * @function setMethods
     * @param {string[]} methods - The methods to populate the interface with
     */
    this.setMethods = setMethods;

    /**
     * Sets the currently selected index by delta.
     * @function moveSelected
     * @param {int} delta
     */
    this.moveSelected = moveSelected;

    /**
     * Sets the position of the UI element.
     * @function setPosition
     * @param {int} left
     * @param {int} top
     */
    this.setPosition = setPosition;

    /** 
     * Provides common keyboard event handling for a keydown event.
     * 
     * escape, left, right -> hide the UI
     * up -> select previous item
     * down -> select next item
     * pageup -> select previous 5th
     * pagedown -> select next 5th
     * 
     * @function handleKeyDown
     * @param {HTMLEvent} evt - The event
     */
    this.handleKeyDown = handleKeyDown;

    /**
     * Adds an event listener for the `onVisibleChanged` event
     * @function onVisibleChanged
     * @param {function} callback
     */
    this.onVisibleChanged = onVisibleChanged;
};

/**
 * Provides a user interface for a declarations popup. This class basically
 * generates a div that acts as a list of items. When items are displayed (usually
 * triggered by a keyboard event), the user can select an item from the list.
 * 
 * @class DeclarationsIntellisense
 */
var DeclarationsIntellisense = function ()
{
    var events = { itemChosen: [], itemSelected: [], visibleChanged: [] };
    var utils = new Utils();
    var selectedIndex = 0;
    var filteredDeclarations = [];
    var filteredDeclarationsUI = [];
    var visible = false;
    var declarations = [];
    var filterText = '';
    var filterModes =
        {
            startsWith: function (item, filterText)
            {
                return item.name.toLowerCase().indexOf(filterText) === 0;
            },
            contains: function (item, filterText)
            {
                return item.name.toLowerCase().indexOf(filterText) >= 0;
            }
        };
    var filterMode = filterModes.startsWith;

    // ui widgets
    var selectedElement = null;
    var listElement = document.createElement('ul');
    listElement.className = 'br-intellisense';

    var documentationElement = document.createElement('div');
    documentationElement.className = 'br-documentation';

    document.body.appendChild(listElement);
    document.body.appendChild(documentationElement);

    function handleKeyDown(evt)
    {
        // escape
        if (evt.keyCode == 27)
        {
            setVisible(false);
            evt.preventDefault();
            evt.cancelBubble = true;
        }
            // left, right
        else if (evt.keyCode === 27 || evt.keyCode === 37 || evt.keyCode === 39)
        {
            setVisible(false);
        }
            // up
        else if (evt.keyCode === 38)
        {
            moveSelected(-1);
            evt.preventDefault();
            evt.cancelBubble = true;
        }
            // down
        else if (evt.keyCode === 40)
        {
            moveSelected(1);
            evt.preventDefault();
            evt.cancelBubble = true;
        }
            // page up 
        else if (evt.keyCode === 33)
        {
            moveSelected(-5);
            evt.preventDefault();
            evt.cancelBubble = true;
        }
            // page down
        else if (evt.keyCode === 34)
        {
            moveSelected(5);
            evt.preventDefault();
            evt.cancelBubble = true;
        }
            // trigger item chosen
        else if (evt.keyCode === 13 || evt.keyCode === 9)
        {
            triggerItemChosen(getSelectedItem());
            evt.preventDefault();
            evt.cancelBubble = true;
        }
    }

    function triggerVisibleChanged()
    {
        events.visibleChanged.forEach(function (callback)
        {
            callback(visible);
        });
    }

    function triggerItemChosen(item)
    {
        events.itemChosen.forEach(function (callback)
        {
            callback(item);
        });
    }

    function triggerItemSelected(item)
    {
        events.itemSelected.forEach(function (callback)
        {
            callback(item);
        });
    }

    function getSelectedIndex()
    {
        return selectedIndex;
    }

    function setSelectedIndex(idx)
    {
        if (idx !== selectedIndex)
        {
            selectedIndex = idx;
            triggerItemSelected(getSelectedItem());
        }
    }

    function onItemChosen(callback)
    {
        events.itemChosen.push(callback);
    }

    function onItemSelected(callback)
    {
        events.itemSelected.push(callback);
    }

    function onVisibleChanged(callback)
    {
        events.visibleChanged.push(callback);
    }

    function getSelectedItem()
    {
        return filteredDeclarations[selectedIndex];
    }

    function createListItemDefault(item)
    {
        var listItem = document.createElement('li');
        listItem.innerHTML = '<span class="br-icon icon-glyph-' + item.glyph + '"></span> ' + item.name;
        listItem.className = 'br-listlink';
        return listItem;
    }

    function refreshSelected()
    {
        if (selectedElement != null)
        {
            utils.removeCssClass(selectedElement, 'br-selected');
        }

        selectedElement = filteredDeclarationsUI[selectedIndex];
        if (selectedElement)
        {
            utils.addCssClass(selectedElement, 'br-selected');

            var item = getSelectedItem();
            if (item.documentation == null)
            {
                showDocumentation(false);
            }
            else
            {
                showDocumentation(true);
                documentationElement.innerHTML = item.documentation;
            }

            var top = selectedElement.offsetTop;
            var bottom = top + selectedElement.offsetHeight;
            var scrollTop = listElement.scrollTop;
            if (top <= scrollTop)
            {
                listElement.scrollTop = top;
            }
            else if (bottom >= scrollTop + listElement.offsetHeight)
            {
                listElement.scrollTop = bottom - listElement.offsetHeight;
            }
        }
    }

    function refreshUI()
    {
        listElement.innerHTML = '';
        filteredDeclarationsUI = [];
        filteredDeclarations.forEach(function (item, idx)
        {
            var listItem = createListItemDefault(item);

            listItem.ondblclick = function ()
            {
                setSelectedIndex(idx);
                triggerItemChosen(getSelectedItem());
                setVisible(false);
                showDocumentation(false);
            };

            listItem.onclick = function ()
            {
                setSelectedIndex(idx);
            };

            listElement.appendChild(listItem);
            filteredDeclarationsUI.push(listItem);
        });

        refreshSelected();
    }

    function showDocumentation(b)
    {
        utils.showElement(documentationElement, b);
    }

    function setVisible(b)
    {
        if (visible !== b)
        {
            visible = b;
            utils.showElement(listElement, b);
            utils.showElement(documentationElement, b);
            triggerVisibleChanged();
        }
    }

    function setDeclarations(data)
    {
        if (data != null && data.length > 0)
        {
            // set the data
            declarations = data;
            filteredDeclarations = data;

            // show the elements
            setSelectedIndex(0);
            setFilter('');
            setVisible(true);
            showDocumentation(true);
        }
    }

    function setPosition(left, top)
    {
        // reposition intellisense
        listElement.style.left = left + 'px';
        listElement.style.top = top + 'px';

        // reposition documentation (magic number offsets can't figure out why)
        documentationElement.style.left = (left + listElement.offsetWidth + 5) + 'px';
        documentationElement.style.top = (top + 5) + 'px';
    }

    function setFilterMode(mode)
    {
        if (typeof (mode) === 'function')
        {
            filterMode = mode;
        }
        else if (typeof (mode) === 'string')
        {
            filterMode = filterModes[mode];
        }
    }

    function setFilter(f)
    {
        if (filterText !== f)
        {
            setSelectedIndex(0);
            filterText = f;
        }

        var ret = [];
        var lowerFilter = filterText.toLowerCase();
        declarations.forEach(function (item)
        {
            if (filterMode(item, lowerFilter))
            {
                ret.push(item);
            }
        });

        filteredDeclarations = ret;
        refreshUI();
    }

    function moveSelected(delta)
    {
        var idx = selectedIndex + delta;
        idx = Math.max(idx, 0);
        idx = Math.min(idx, filteredDeclarations.length - 1);

        // select
        setSelectedIndex(idx);
        refreshSelected();
    }

    function isVisible()
    {
        return visible;
    }

    /** 
     * Setter for the filter text. When set, the items displayed are
     * automatically filtered
     * 
     * @param {string} f - The filter to set
     * @function setFilter
     */
    this.setFilter = setFilter;

    /** 
     * Setter for how the filter behaves. There are two default implementations
     * startsWith and contains. 
     * 
     * The `startsWith` mode checks that the `name` property
     * of the item starts with the filter text
     * 
     * The `contains` mode checks for any 
     * substring of the filter text in the `name` property of the item.
     * 
     * @param {string|function(item, string)} mode - The mode to set
     * @function setFilterMode
     */
    this.setFilterMode = setFilterMode;

    /**
     * Gets the selected item
     * @returns {DeclartionItem}
     * @function getSelectedItem
     */
    this.getSelectedItem = getSelectedItem;

    /**
     * Gets the currently selected index
     * @returns {int}
     * @function getSelectedIndex
     */
    this.getSelectedIndex = getSelectedIndex;

    /**
     * Sets the currently selected index
     * @param {int} idx
     * @function setSelectedIndex
     */
    this.setSelectedIndex = setSelectedIndex;

    /**
     * Adds an event listener for the `onItemChosen` event
     * @param {function} callback
     * @function onItemChosen
     */
    this.onItemChosen = onItemChosen;

    /**
     * Adds an event listener for the `onItemSelected` event
     * @param {function} callback
     * @function onItemSelected
     */
    this.onItemSelected = onItemSelected;

    /**
     * Adds an event listener for the `onVisibleChanged` event
     * @param {function} callback
     * @function onVisibleChanged
     */
    this.onVisibleChanged = onVisibleChanged;

    /**
     * Sets the currently selected index by delta.
     * @param {int} delta
     * @function moveSelected
     */
    this.moveSelected = moveSelected;

    /**
     * Sets the declarations to display. If not empty, the user interface is shown and the
     * first item is selected.
     * @param {DeclartionItem[]} data - The array of declaration items to show
     * @function setDeclarations
     */
    this.setDeclarations = setDeclarations;

    /**
     * Sets the position of the UI element.
     * @param {int} left
     * @param {int} top
     * @function setPosition
     */
    this.setPosition = setPosition;

    /**
     * Checks to see if the UI is visible
     * @function setVisible
     * @returns {boolean}
     */
    this.setVisible = setVisible;

    /** 
     * Check to see if the declarations div is visible 
     * @function isVisible
     * @returns {boolean}
     */
    this.isVisible = isVisible;

    /** 
     * Provides common keyboard event handling for a keydown event.
     * 
     * escape, left, right -> hide the UI
     * up -> select previous item
     * down -> select next item
     * pageup -> select previous 5th
     * pagedown -> select next 5th
     * enter, tab -> chooses the currently selected item
     * 
     * @param {HTMLEvent} evt - The event
     * @function handleKeyDown
     */
    this.handleKeyDown = handleKeyDown;
};