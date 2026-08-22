# Comparison line model

This directory contains the byte-preserving line-document model and normalization policy shared by line-oriented GNU Diffutils commands.

The reader records whether each final line was terminated, while normalization applies comparison-only case, whitespace, tab, carriage-return, blank-line, and matching-line policies without changing the original bytes used for output.
