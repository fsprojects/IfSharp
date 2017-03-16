define(function () {

    var kernelSpecs = requirejs.s.contexts._.config.paths.kernelspecs;
    var staticFolder = kernelSpecs + "/ifsharp/static/";

    var link = document.createElement("link");
    link.type = "text/css";
    link.rel = "stylesheet";
    link.href = staticFolder + "custom/fsharp.css";
    document.getElementsByTagName("head")[0].appendChild(link);

    var changedSinceTypecheck = true;
    var changedRecently = false;
    var kernelIdledSinceTypeCheck = false;
    var kernelIdledRecently = false

    var onload = function () {

        var md = IPython.notebook.metadata;
        if (md.language) {
            console.log('language already defined and is :', md.language);
        }
        else {
            md.language = 'fsharp';
            console.log('add metadata hint that language is fsharp...');
        }

        IPython.CodeCell.options_default.cm_config.mode = 'fsharp';

        // callback called by the end-user
        function updateMarkers(data) {
            // applies intellisense hooks onto all cells
            var cells = getCodeCells();

            // clear old error marks
            cells.cells.forEach(function (cell) {
                cell.code_mirror.doc.getAllMarks().forEach(function (m) {
                    if (m.className === 'br-errormarker') {
                        m.clear();
                    }
                });
            });

            // apply new error marks
            data.forEach(function (err) {
                var editor = cells.cells[err.CellNumber].code_mirror;
                var from = { line: err.StartLine, ch: err.StartColumn };
                var to = { line: err.EndLine, ch: err.EndColumn };
                editor.doc.markText(from, to, { title: err.Message, className: 'br-errormarker' });
            });
        }

        function getCodeCells() {
            var results = { codes: [], cells: [], selectedCell: null, selectedIndex: 0 };
            IPython.notebook.get_cells()
                .forEach(function (c) {
                    if (c.cell_type === 'code') {
                        if (c.selected === true) {
                            results.selectedCell = c;
                            results.selectedIndex = results.cells.length;
                        }
                        results.cells.push(c);
                        results.codes.push(c.code_mirror.getValue());
                    }
                });

            return results;
        }

        function intellisenseRequest(item) {
            var cells = getCodeCells();
            var editor = cells.selectedCell != null ? cells.selectedCell.code_mirror : null
            var cursor = editor != null ? editor.doc.getCursor() : { ch: 0, line: 0 }
            var callbacks = { shell: {}, iopub: {} };

            if (editor != null) {
                var line = editor.getLine(cursor.line);
                var isSlash = item.keyCode === 191 || item.keyCode === 220;
                var isQuote = item.keyCode === 222;

                var isLoadOrRef = line.indexOf('#load') === 0
                    || line.indexOf('#r') === 0;

                var isStartLoadOrRef = line === '#load "'
                    || line === '#r "'
                    || line === '#load @"'
                    || line === '#r @"';

                if (isSlash && !isLoadOrRef) {
                    return;
                }

                if (isQuote && !isStartLoadOrRef) {
                    return;
                }
            }

            callbacks.shell.reply = function (msg) {
                if (editor != null && item.keyCode !== 0) {
                    editor.intellisense.setDeclarations(msg.content.matches);
                    editor.intellisense.setStartColumnIndex(msg.content.cursor_start);
                }
            };

            callbacks.iopub.output = function (msg) {
                updateMarkers(msg.content.data.errors);
            };

            var content = {
                text: JSON.stringify(cells.codes),
                line: '',
                block: JSON.stringify({ selectedIndex: cells.selectedIndex, ch: cursor.ch, line: cursor.line }),
                cursor_pos: cursor.ch
            };

            IPython.notebook.kernel.send_shell_message("intellisense_request", content, callbacks, null, null);
        }

        //There are dependencies in the lazy loading 
        require(['codemirror/addon/mode/loadmode'], function () {
            require([staticFolder + 'custom/fsharp.js'], function () {
                require([staticFolder + 'custom/webintellisense.js', staticFolder + 'custom/webintellisense-codemirror.js'], function () {
                    // applies intellisense hooks onto a cell
                    function applyIntellisense(cell) {
                        if (cell.cell_type !== 'code') { return; }

                        var editor = cell.code_mirror;
                        if (editor.intellisense == null) {
                            var intellisense = new CodeMirrorIntellisense(editor);
                            editor.setOption('theme', 'neat');
                            editor.intellisense = intellisense;

                            editor.on('changes', function (cm, changes) {
                                changedSinceTypecheck = true;
                                changedRecently = true;
                            });

                            intellisense.addDeclarationTrigger({ keyCode: 190 }); // `.`
                            intellisense.addDeclarationTrigger({ keyCode: 32, ctrlKey: true, preventDefault: true, type: 'down' }); // `ctrl+space`
                            intellisense.addDeclarationTrigger({ keyCode: 191 }); // `/`
                            intellisense.addDeclarationTrigger({ keyCode: 220 }); // `\`
                            intellisense.addDeclarationTrigger({ keyCode: 222 }); // `"`
                            intellisense.addDeclarationTrigger({ keyCode: 222, shiftKey: true }); // `"`
                            intellisense.addMethodsTrigger({ keyCode: 57, shiftKey: true }); // `(`
                            intellisense.addMethodsTrigger({ keyCode: 48, shiftKey: true });// `)`
                            intellisense.onMethod(function (item) { });
                            intellisense.onDeclaration(intellisenseRequest);
                        }
                    }

                    // applies intellisense hooks onto all cells
                    IPython.notebook.get_cells().forEach(function (cell) {
                        applyIntellisense(cell);
                    });

                    // applies intellisense hooks onto cells that are selected
                    $([IPython.events]).on('create.Cell', function (event, data) {
                        applyIntellisense(data.cell);
                    });

                    $([IPython.events]).on('delete.Cell', function (event, data) {
                        data.cell.code_mirror.intellisense.setDeclarations([])
                    });

                    $([IPython.events]).on('kernel_idle.Kernel', function (event, data) {
                        kernelIdledSinceTypeCheck = true;
                        kernelIdledRecently = true;
                    });



                    window.setInterval(function () {
                        if (!changedSinceTypecheck && !kernelIdledSinceTypeCheck)
                            return;

                        if (changedRecently || kernelIdledRecently) {
                            changedRecently = false;
                            kernelIdledRecently = false;
                            return;
                        }

                        changedSinceTypecheck = false;
                        changedRecently = false;
                        kernelIdledSinceTypeCheck = false;
                        kernelIdledRecently = false;
                        intellisenseRequest({ keyCode: 0 })
                        console.log("requesting intellisense");
                    }, 1000);

                });
            });
        });
    }

    return { onload: onload }
})