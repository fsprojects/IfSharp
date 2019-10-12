#!/usr/bin/env bash

set -eu
set -o pipefail

# workaround macOS build agent issue from https://github.com/dotnet/cli/issues/9114#issuecomment-494226139
export DOTNET_ROOT="$(dirname "$(readlink "$(command -v dotnet)")")"

# liberated from https://stackoverflow.com/a/18443300/433393
realpath() {
  OURPWD=$PWD
  cd "$(dirname "$1")"
  LINK=$(readlink "$(basename "$1")")
  while [ "$LINK" ]; do
    cd "$(dirname "$LINK")"
    LINK=$(readlink "$(basename "$1")")
  done
  REALPATH="$PWD/$(basename "$1")"
  cd "$OURPWD"
  echo "$REALPATH"
}

TOOL_PATH=$(realpath .fake)
FAKE="$TOOL_PATH"/fake

if ! [ -e "$FAKE" ]
then
  dotnet tool install fake-cli --tool-path "$TOOL_PATH"
fi
"$FAKE" "$@"
