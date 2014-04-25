$([IPython.events]).on('notebook_loaded.Notebook', function ()
{
    var md = IPython.notebook.metadata;
    if (md.language)
    {
        console.log('language already defined and is :', md.language);
    }
    else
    {
        md.language = 'fsharp';
        console.log('add metadata hint that language is fsharp...');
    }
});

$([IPython.events]).on('app_initialized.NotebookApp', function ()
{
    require(['custom/fsharp']);

    IPython.CodeCell.options_default.cm_config.mode = 'fsharp';

    // callback called by the end-user
    function updateMarkers(data)
    {
        // applies intellisense hooks onto all cells
        var cells = getCodeCells();
        data.forEach(function (err)
        {
            var cell = cells.cells[err.CellNumber]
            var editor = cell.code_mirror;

            // clear our error marks
            editor.doc.getAllMarks()
                .forEach(function (m)
                {
                    if (m.className === 'br-errormarker')
                    {
                        m.clear();
                    }
                });

            var from = { line: err.StartLine, ch: err.StartColumn };
            var to = { line: err.EndLine, ch: err.EndColumn };
            editor.doc.markText(from, to, { title: err.Message, className: 'br-errormarker' });
        });
    }

    function getCodeCells()
    {
        var results = { codes: [], cells: [], selectedCell: null, selectedIndex: 0 };
        IPython.notebook.get_cells()
            .forEach(function (c)
            {
                if (c.cell_type === 'code')
                {
                    if (c.selected === true)
                    {
                        results.selectedCell = c;
                        results.selectedIndex = results.cells.length;
                    }
                    results.cells.push(c);
                    results.codes.push(c.code_mirror.getValue());
                }
            });

        return results;
    }

    require(['custom/codemirror-intellisense'], function ()
    {
        // applies intellisense hooks onto a cell
        function applyIntellisense(cell)
        {
            if (cell.cell_type !== 'code') { return; }

            var editor = cell.code_mirror;
            if (editor.intellisense == null)
            {
                cell.force_highlight('fsharp');
                cell.code_mirror.setOption('theme', 'neat');
                editor.intellisense = new Intellisense(editor, function (callback, position)
                {
                    var cells = getCodeCells();
                    var codes = cells.codes;
                    var cursor = cells.selectedCell.code_mirror.doc.getCursor();
                    var callbacks = { shell: {}, iopub: {} };

                    // v2
                    callbacks.shell.reply = function (data)
                    {
                        callback(data.content.matches);
                    };

                    callbacks.iopub.output = function (msg)
                    {
                        updateMarkers(msg.content.data.errors);
                    };

                    // v1
                    callbacks.complete_reply = function (data)
                    {
                        callback(data.matches);
                    };

                    callbacks.output = function (msgType, content, metadata)
                    {
                        updateMarkers(content.data.errors);
                    };

                    var content = {
                        text: JSON.stringify(codes),
                        line: '',
                        block: JSON.stringify({ selectedIndex: cells.selectedIndex, ch: cursor.ch, line: cursor.line }),
                        cursor_pos: cursor.ch
                    };
                    var msg = IPython.notebook.kernel._get_msg("complete_request", content);
                    IPython.notebook.kernel.shell_channel.send(JSON.stringify(msg));
                    IPython.notebook.kernel.set_callbacks_for_msg(msg.header.msg_id, callbacks);
                });
            }
        }

        // applies intellisense hooks onto all cells
        IPython.notebook.get_cells()
            .forEach(function (cell)
            {
                applyIntellisense(cell);
            });

        // applies intellisense hooks onto cells that are selected
        $([IPython.events]).on('create.Cell', function (event, data)
        {
            applyIntellisense(data.cell);
        });
    });

    // replace the image
    var img = $('.container img')[0];
    img.src = "/static/custom/ifsharp_logo.png";
});