#!/usr/bin/env sh
set -eu

CONFIGURATION="${2:-Debug}"

case "$CONFIGURATION" in
    Debug|Staging|Release)
        ;;

    *)
        printf 'Invalid configuration: %s\n' "$CONFIGURATION" >&2
        printf 'Usage: %s [clean|restore|build|test] [Debug|Staging|Release]\n' "$0" >&2
        exit 1
        ;;
esac

clean()
{
    printf '\n=== Clean (%s) ===\n' "$CONFIGURATION"
    dotnet clean Icod.CoreUtils.sln -c "$CONFIGURATION"
}

restore()
{
    printf '\n=== Restore ===\n'
    dotnet restore Icod.CoreUtils.sln
}

build()
{
    printf '\n=== Build (%s) ===\n' "$CONFIGURATION"
    dotnet build Icod.CoreUtils.sln -c "$CONFIGURATION" --no-restore
}

test()
{
    printf '\n=== Test (%s) ===\n' "$CONFIGURATION"
    dotnet test Icod.CoreUtils.sln  \
        -c "$CONFIGURATION" \
        --no-build
}

case "${1-}" in
    "")
        clean
        restore
        build
        test
        ;;

    clean)
        clean
        ;;

    restore)
        restore
        ;;

    build)
        build
        ;;

    test)
        test
        ;;

    *)
        printf 'Invalid section: %s\n' "$1" >&2
        printf 'Usage: %s [clean|restore|build|test] [Debug|Staging|Release]\n' "$0" >&2
        exit 1
        ;;
esac
