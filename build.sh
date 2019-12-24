#!/usr/bin/env bash
dotnet tool restore
source fake.sh build "$@"