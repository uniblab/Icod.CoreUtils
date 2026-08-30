# ID(1)

## NAME

**id** — print user, group, and security-context information

## SYNOPSIS

```text
id [OPTION]... [USER]...
```

## DESCRIPTION

`Icod.CoreUtils.ID` is a managed .NET implementation of GNU Coreutils `id(1)`, modeled on GNU Coreutils 9.11.

With no user operand, the command reports the current process identity. The default form includes real user and group identities, effective identities when they differ, supplementary groups, and a security context when the active identity provider exposes one.

Named users may be resolved by name or provider-specific identifier. When several users are supplied, each is processed independently and a missing user does not prevent later users from being examined.

## OPTIONS

```text
-a
    Accepted and ignored for compatibility with other implementations.

-Z, --context
    Print only the current security context.

-g, --group
    Print only the effective group ID.

-G, --groups
    Print all group IDs.

-n, --name
    Print names rather than identifiers with -u, -g, or -G.

-r, --real
    Print the real rather than effective identity with -u, -g, or -G.

-u, --user
    Print only the effective user ID.

-z, --zero
    Use NUL delimiters rather than whitespace where the selected output form
    permits them.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

Only one of `--context`, `--group`, `--groups`, and `--user` may select an "only" output form. `--name` and `--real` require `-u`, `-g`, or `-G`. `--zero` is not permitted with the default composite format.

`--context` cannot be combined with a user operand and succeeds only when the identity provider exposes a nonempty security context.

## EXIT STATUS

```text
0    All requested identity information was written successfully.
1    Usage was invalid, a requested user or security context was unavailable,
     or another identity/output operation failed.
130  The operation was cancelled.
```

## PLATFORM NOTES

Identity discovery is supplied through the shared `IIdentityProvider` abstraction rather than by directly parsing Unix account databases in the command. Numeric or textual identity values therefore follow the active platform provider.

SELinux-style `--context` output is capability-dependent. On systems where the provider exposes no security context, the command reports that the option requires an SELinux-enabled kernel instead of fabricating a value.

## PATHNAME GLOBBING

`id` does not perform `Icod.CommandFramework` pathname glob expansion. Its user operands identify accounts rather than filesystem pathnames, so `*`, `?`, and `**` are not interpreted as pathname patterns by `id`. An invoking shell or other caller may still expand arguments before the program receives them.

## AUTHORS

GNU `id` was written by Arnold Robbins and David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`id(1)`, `groups(1)`, `whoami(1)`, `logname(1)`
