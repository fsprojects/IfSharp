namespace IfSharp.Kernel

open System.Collections.Generic

type ConnectionInformation = 
    {
        stdin_port: int;
        ip: string;
        control_port: int;
        hb_port: int;
        signature_scheme: string;
        key: string;
        shell_port: int;
        transport: string;
        iopub_port: int;
    }

type IntellisenseItem = 
    {
        glyph: int
        name: string
        documentation: string
        value: string
    }