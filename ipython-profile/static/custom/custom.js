$([IPython.events]).on('app_initialized.NotebookApp', function ()
{
    require(['custom/fsharp']);
    require(['custom/codemirror-intellisense'], function ()
    {
        // applies intellisense hooks onto a cell
        function applyIntellisense(cell)
        {
            var editor = cell.code_mirror;
            if (editor.intellisense == null)
            {
                editor.intellisense = new Intellisense(editor, function (callback, position)
                {
                    console.log('It is happening', position);

                    var selectedIndex = 0;
                    var selectedCell = null;
                    var codes = [];
                    IPython.notebook.get_cells()
                        .forEach(function (c, idx)
                        {
                            if (c.selected === true)
                            {
                                selectedCell = c;
                                selectedIndex = idx;
                            }
                            codes.push(c.code_mirror.getValue());
                        });

                    var cursor = selectedCell.code_mirror.doc.getCursor();
                    var callbacks = {};
                    callbacks.complete_reply = function (data)
                    {
                        callback(data.matches);
                    };

                    var content = {
                        text: JSON.stringify(codes),
                        line: '',
                        block: JSON.stringify({ selectedIndex: selectedIndex, ch: cursor.ch, line: cursor.line }),
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
    var img = $('.container img')[0]
    img.src = "/static/custom/ifsharp_logo.png"
});