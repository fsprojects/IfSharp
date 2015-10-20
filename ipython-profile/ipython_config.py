c = get_config()
c.KernelManager.kernel_spec = [ "mono", r"%kexe", "{connection_file}"]
c.Session.key = b''
c.Session.keyfile = ''
c.NotebookApp.extra_static_paths = [ r"%kfolder" ]