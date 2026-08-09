/* Native preload helper for Icod.CoreUtils stdbuf. */

#include <errno.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static void apply_mode(FILE *stream, const char *stream_name, const char *variable_name)
{
    const char *mode = getenv(variable_name);
    char *buffer = NULL;
    int buffering_mode;
    size_t buffer_size = 0;

    if (mode == NULL)
        return;

    if (strcmp(mode, "0") == 0)
    {
        buffering_mode = _IONBF;
    }
    else if (strcmp(mode, "L") == 0)
    {
        buffering_mode = _IOLBF;
    }
    else
    {
        char *end = NULL;
        unsigned long long parsed;

        errno = 0;
        parsed = strtoull(mode, &end, 10);
        if (errno != 0 || end == mode || *end != '\0' || parsed == 0 || parsed > (unsigned long long)(SIZE_MAX / 2))
        {
            fprintf(stderr, "stdbuf: invalid or unsupported buffer size %s for %s\n", mode, stream_name);
            return;
        }

        buffer_size = (size_t)parsed;
        buffer = (char *)malloc(buffer_size);
        if (buffer == NULL)
        {
            fprintf(stderr, "stdbuf: unable to allocate %zu bytes for %s buffering\n", buffer_size, stream_name);
            return;
        }
        buffering_mode = _IOFBF;
    }

    if (setvbuf(stream, buffer, buffering_mode, buffer_size) != 0)
    {
        fprintf(stderr, "stdbuf: unable to set %s buffering mode %s\n", stream_name, mode);
        free(buffer);
    }
}

static void __attribute__((constructor)) initialize_stdbuf(void)
{
    /* Configure stderr first so diagnostics from the remaining setup use it. */
    apply_mode(stderr, "standard error", "_STDBUF_E");
    apply_mode(stdin, "standard input", "_STDBUF_I");
    apply_mode(stdout, "standard output", "_STDBUF_O");
}
