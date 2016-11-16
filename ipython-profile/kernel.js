define(function () {

    var kernelSpecs = requirejs.s.contexts._.config.paths.kernelspecs;
    var staticFolder = kernelSpecs + "/ifsharp/static/";

    var link = document.createElement("link");
    link.type = "text/css";
    link.rel = "stylesheet";
    link.href = staticFolder + "custom/fsharp.css";
    document.getElementsByTagName("head")[0].appendChild(link);

    require(['codemirror/addon/mode/loadmode']);

    var onload = function () {

        var md = IPython.notebook.metadata;
        if (md.language) {
            console.log('language already defined and is :', md.language);
        }
        else {
            md.language = 'fsharp';
            console.log('add metadata hint that language is fsharp...');
        }

        require([staticFolder + 'custom/fsharp.js']);

        IPython.CodeCell.options_default.cm_config.mode = 'fsharp';

        // callback called by the end-user
        function updateMarkers(data) {
            // applies intellisense hooks onto all cells
            var cells = getCodeCells();
            data.forEach(function (err) {
                var cell = cells.cells[err.CellNumber]
                var editor = cell.code_mirror;

                // clear our error marks
                editor.doc.getAllMarks()
                    .forEach(function (m) {
                        if (m.className === 'br-errormarker') {
                            m.clear();
                        }
                    });

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

        require([staticFolder + 'custom/webintellisense.js', staticFolder + 'custom/webintellisense-codemirror.js'], function () {
            // applies intellisense hooks onto a cell
            function applyIntellisense(cell) {
                if (cell.cell_type !== 'code') { return; }

                var editor = cell.code_mirror;
                if (editor.intellisense == null) {
                    var intellisense = new CodeMirrorIntellisense(editor);
                    cell.force_highlight('fsharp');
                    cell.code_mirror.setOption('theme', 'neat');
                    editor.intellisense = intellisense;

                    intellisense.addDeclarationTrigger({ keyCode: 190 }); // `.`
                    intellisense.addDeclarationTrigger({ keyCode: 32, ctrlKey: true, preventDefault: true, type: 'down' }); // `ctrl+space`
                    intellisense.addDeclarationTrigger({ keyCode: 191 }); // `/`
                    intellisense.addDeclarationTrigger({ keyCode: 220 }); // `\`
                    intellisense.addDeclarationTrigger({ keyCode: 222 }); // `"`
                    intellisense.addDeclarationTrigger({ keyCode: 222, shiftKey: true }); // `"`
                    intellisense.addMethodsTrigger({ keyCode: 57, shiftKey: true }); // `(`
                    intellisense.addMethodsTrigger({ keyCode: 48, shiftKey: true });// `)`
                    intellisense.onMethod(function (item, position) {

                    });
                    intellisense.onDeclaration(function (item, position) {
                        var cells = getCodeCells();
                        var codes = cells.codes;
                        var cursor = cells.selectedCell.code_mirror.doc.getCursor();
                        var callbacks = { shell: {}, iopub: {} };
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

                        // v2
                        callbacks.shell.reply = function (msg) {
                            intellisense.setDeclarations(msg.content.matches);
                            if (msg.content.filter_start_index)
                                intellisense.setStartColumnIndex(msg.content.filter_start_index);
                        };

                        callbacks.iopub.output = function (msg) {
                            updateMarkers(msg.content.data.errors);
                        };

                        // v1
                        callbacks.complete_reply = function (data) {
                            intellisense.setDeclarations(data.matches);
                            intellisense.setStartColumnIndex(data.filter_start_index);
                        };

                        callbacks.output = function (msgType, content, metadata) {
                            updateMarkers(content.data.errors);
                        };

                        var content = {
                            text: JSON.stringify(codes),
                            line: '',
                            block: JSON.stringify({ selectedIndex: cells.selectedIndex, ch: cursor.ch, line: cursor.line }),
                            cursor_pos: cursor.ch
                        };

                        IPython.notebook.kernel.send_shell_message("intellisense_request", content, callbacks, null, null);
                    });
                }
            }

            // applies intellisense hooks onto all cells
            IPython.notebook.get_cells()
                .forEach(function (cell) {
                    applyIntellisense(cell);
                });

            // applies intellisense hooks onto cells that are selected
            $([IPython.events]).on('create.Cell', function (event, data) {
                applyIntellisense(data.cell);
            });
        });

        // replace the image
        var img = $('.container img')[0];
        img.src = staticFolder + "custom/ifsharp_logo.png";

    }

    return { onload: onload }
})