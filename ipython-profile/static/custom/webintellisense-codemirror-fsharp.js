var Intellisense = function (editor, userCallback)
{
    var self = this;

    // data element
    self.selectedIndex = 0;
    self.isAutoCompleteOpen = false;
    self.filteredDeclarations = [];
    self.filteredDeclarationsUI = [];
    self.declarations = []
    self.autoCompleteStart = { line: 0, ch: 0 };

    // ui widgets
    self.selectedElement = null;
    self.listElement = document.createElement('ul');
    self.listElement.className = 'br-intellisense';

    self.documentationElement = document.createElement('div');
    self.documentationElement.className = 'br-documentation';
    document.body.appendChild(self.listElement);
    document.body.appendChild(self.documentationElement);

    // filters an array
    function filter(arr, cb)
    {
        var ret = [];
        arr.forEach(function (item)
        {
            if (cb(item))
            {
                ret.push(item);
            }
        });
        return ret;
    }

    // creates a list item that is appended to our intellisense list
    function createListItemDefault(item, idx)
    {
        var listItem = document.createElement('li');
        listItem.innerHTML = '<span class="br-icon icon-glyph-' + item.glyph + '"></span> ' + item.name;
        listItem.className = 'br-listlink'
        return listItem;
    }

    // inserts the currently selected auto complete
    self.insertAutoComplete = function ()
    {
        if (self.isAutoCompleteOpen)
        {
            var selectedDeclaration = self.filteredDeclarations[self.selectedIndex];
            if (selectedDeclaration == null)
            {
                self.showAutoComplete(false);
            }
            else
            {
                var cursor = editor.doc.getCursor();
                var line = editor.doc.getLine(self.autoCompleteStart.line);
                var name = selectedDeclaration.name;
                if (lastIndexOfAny(name, [' ', '[', ']', '.']) != -1)
                {
                    name = '``' + name + '``';
                }
                var newLine = line.substring(0, self.autoCompleteStart.ch)
                    + name
                    + line.substring(cursor.ch, line.length);

                editor.doc.setLine(cursor.line, newLine);
                editor.setSelection({ line: cursor.line, ch: cursor.ch + name.length });
                self.showAutoComplete(false);
                editor.focus();
            }
        }
    };

    // refreshes the user interface for the selected element
    self.refreshSelected = function ()
    {
        if (self.selectedElement != null)
        {
            removeCssClass(self.selectedElement, 'br-selected');
        }

        self.selectedElement = self.filteredDeclarationsUI[self.selectedIndex];
        if (self.selectedElement)
        {
            addCssClass(self.selectedElement, 'br-selected');
            self.documentationElement.innerHTML = self.filteredDeclarations[self.selectedIndex].documentation;

            var top = self.selectedElement.offsetTop;
            var bottom = top + self.selectedElement.offsetHeight;
            var scrollTop = self.listElement.scrollTop;
            if (top <= scrollTop)
            {
                self.listElement.scrollTop = top;
            }
            else if (bottom >= scrollTop + self.listElement.offsetHeight)
            {
                self.listElement.scrollTop = bottom - self.listElement.offsetHeight;
            }
        }
    };

    // refreshes the DOM
    self.refreshUI = function ()
    {
        self.listElement.innerHTML = '';
        self.filteredDeclarationsUI = [];
        self.filteredDeclarations.forEach(function (item, idx)
        {
            var listItem = createListItemDefault(item, idx);

            listItem.ondblclick = function ()
            {
                self.selectedIndex = idx;
                self.insertAutoComplete();
            };

            listItem.onclick = function ()
            {
                self.selectedIndex = idx;
                self.refreshSelected();
                editor.focus();
            };

            self.listElement.appendChild(listItem);
            self.filteredDeclarationsUI.push(listItem);
        });

        self.refreshSelected();
    };

    // requests that the user provide items to display in the intellisense popup
    self.autoComplete = function ()
    {
        if (typeof (userCallback) === 'function')
        {
            var cursor = editor.doc.getCursor();
            var line = editor.doc.getLine(cursor.line);
            var find = lastIndexOfAny(line, [' ', '\t', '.'], cursor.ch) + 1;
            self.autoCompleteStart = { line: cursor.line, ch: find };
            userCallback(self.callback, self.autoCompleteStart);
        }
    };

    // show the auto complete and the documentation elements
    self.showAutoComplete = function (b)
    {
        self.isAutoCompleteOpen = b;
        self.listElement.style.display = b ? 'block' : 'none';
        self.documentationElement.style.display = b ? 'block' : 'none';
    };

    // this method is called by the end-user application
    self.callback = function (data)
    {
        if (data != null && data.length > 0)
        {
            // set the data
            self.declarations = data;
            self.filteredDeclarations = data;

            // refresh the DOM
            self.refreshFilter();

            // set the position of the popup
            var coords = editor.display.cursor.getBoundingClientRect();

            // show the elements
            self.showAutoComplete(true);

            // reposition intellisense
            self.listElement.style.left = coords.left + 'px';
            self.listElement.style.top = coords.bottom + 'px';

            // reposition documentation (magic number offsets can't figure out why)
            self.documentationElement.style.left = (coords.left + self.listElement.offsetWidth + 5) + 'px';
            self.documentationElement.style.top = (coords.bottom + 5) + 'px';
        }
    };

    // moves the auto complete selection up or down a specified amount
    self.moveAutoComplete = function (delta)
    {
        if (self.isAutoCompleteOpen)
        {
            // apply the new selected index
            self.selectedIndex = self.selectedIndex + delta;
            self.selectedIndex = Math.max(self.selectedIndex, 0);
            self.selectedIndex = Math.min(self.selectedIndex, self.filteredDeclarations.length - 1);

            // select
            self.refreshSelected();
        }
    }

    // refresh the filter
    self.refreshFilter = function ()
    {
        var line = editor.doc.getLine(self.autoCompleteStart.line);
        var filterText = line.substring(self.autoCompleteStart.ch, editor.getCursor().ch).toLowerCase()
        self.filteredDeclarations = filter(self.declarations, function (x)
        {
            return x.name.toLowerCase().indexOf(filterText) === 0;
        });
        self.selectedIndex = 0;
        self.refreshUI();
    };

    editor.doc.on('change', function (cm, changes)
    {
        if (self.isAutoCompleteOpen && (changes.origin === '+delete' || changes.origin === '+input'))
        {
            var cursor = editor.doc.getCursor();
            if (cursor.ch < self.autoCompleteStart.ch)
            {
                self.showAutoComplete(false);
            }
            else
            {
                self.refreshFilter();
            }

            self.refreshFilter();
        }
    });

    editor.on('keydown', function (cm, evt)
    {
        if (self.isAutoCompleteOpen)
        {
            // escape
            if (evt.keyCode === 27)
            {
                self.showAutoComplete(false);
                evt.cancelBubble = true;
            }
            // left
            else if (evt.keyCode === 37)
            {
                self.showAutoComplete(false);
                evt.cancelBubble = true;
            }
            // right
            else if (evt.keyCode === 39)
            {
                self.showAutoComplete(false);
                evt.cancelBubble = true;
            }
            // up
            else if (evt.keyCode === 38)
            {
                self.moveAutoComplete(-1);
                evt.cancelBubble = true;
                evt.preventDefault();
            }
            // down
            else if (evt.keyCode === 40)
            {
                self.moveAutoComplete(1);
                evt.cancelBubble = true;
                evt.preventDefault();
            }
            // page down
            else if (evt.keyCode === 34)
            {
                self.moveAutoComplete(5);
                evt.cancelBubble = true;
                evt.preventDefault();
            }
            // page up
            else if (evt.keyCode === 33)
            {
                self.moveAutoComplete(-5);
                evt.cancelBubble = true;
                evt.preventDefault();
            }
            // tab
            else if (evt.keyCode === 9)
            {
                self.insertAutoComplete();
                evt.cancelBubble = true;
                evt.preventDefault();
            }
            // enter
            else if (evt.keyCode === 13)
            {
                self.insertAutoComplete();
                evt.cancelBubble = true;
                evt.preventDefault();
            }
        }
    });

    var initialKeyMap =
	{
	    'Ctrl-Space': function (cm)
	    {
	        self.autoComplete();
	    },
	    '.': function (cm)
	    {
	        cm.replaceSelection('.', "end", "+input");
	        self.autoComplete();
	    },
	    '(': function (cm)
	    {
	        cm.replaceSelection('(', "end", "+input");
	        self.autoComplete();
	    }
	};

    editor.addKeyMap(initialKeyMap);
};