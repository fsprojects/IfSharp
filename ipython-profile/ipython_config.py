c = get_config()
c.KernelManager.kernel_spec = [ "mono", r"%s", "{connection_file}"]
c.Session.key = b''
c.Session.keyfile = ''