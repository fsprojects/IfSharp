#!/bin/bash

PREFIX="./"

if test "$OS" = "Windows_NT"
then
  # use .Net
  EXE=""
  FLAGS=""
else
  EXE="mono"
  FLAGS="-d:MONO"
fi

${EXE} ${PREFIX}/.paket/paket.bootstrapper.exe
exit_code=$?
if [ $exit_code -ne 0 ]; then
	exit $exit_code
fi

${EXE} ${PREFIX}/.paket/paket.exe restore
exit_code=$?
if [ $exit_code -ne 0 ]; then
	exit $exit_code
fi

${EXE} ${PREFIX}/packages/FAKE/tools/FAKE.exe $@ --fsiargs ${FLAGS} build.fsx 

