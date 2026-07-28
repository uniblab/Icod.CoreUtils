# numfmt

`numfmt` implements the GNU Coreutils 9.11 human-readable number converter. It supports SI and IEC input/output scales, exact decimal parsing, configurable rounding, field selection, headers, delimiters, padding, suffixes, custom `%f` formats, grouping, and NUL-delimited records.

The implementation is managed and platform-neutral. Windows, Linux, and macOS are the required validation platforms; BSD-family behavior is best effort and should be identical except where host locale data differs.
