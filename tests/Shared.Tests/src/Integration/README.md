# Completion Gate C3 composition tests

`C3CompositionTests.cs` verifies that the new components compose without hidden normalization: positional ranges operate across bounded record segments, character ranges select exact C2 UTF-8 source bytes, and parsed GNU `paste` delimiters retain empty elements while cycling.
