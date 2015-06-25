c = get_config()
c.KernelManager.kernel_spec = [r"%s", "{connection_file}"]
c.Session.key = b''
c.Session.keyfile = ''