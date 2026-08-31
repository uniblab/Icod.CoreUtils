#define _POSIX_C_SOURCE 200809L

#include <errno.h>
#include <fcntl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

static int drain_available(int descriptor)
{
    char buffer[32];
    int total = 0;

    for (;;)
    {
        ssize_t count = read(descriptor, buffer, sizeof(buffer));
        if (count > 0)
        {
            total += (int)count;
            continue;
        }
        if (count == 0 || (count < 0 && (errno == EAGAIN || errno == EWOULDBLOCK)))
            return total;
        return -1;
    }
}

int main(int argc, char **argv)
{
    if (argc == 2 && strcmp(argv[1], "--process-id") == 0)
    {
        if (printf("%ld", (long)getpid()) < 0)
            return 9;
        if (fflush(stdout) != 0)
            return 10;
        return 0;
    }

    int descriptors[2];
    int saved_stdout;
    int flags;
    int first_write;
    int newline_write;
    int explicit_flush;

    if (pipe(descriptors) != 0)
        return 2;

    saved_stdout = dup(STDOUT_FILENO);
    if (saved_stdout < 0)
        return 3;

    flags = fcntl(descriptors[0], F_GETFL, 0);
    if (flags < 0 || fcntl(descriptors[0], F_SETFL, flags | O_NONBLOCK) != 0)
        return 4;

    if (dup2(descriptors[1], STDOUT_FILENO) < 0)
        return 5;
    close(descriptors[1]);

    if (fputc('x', stdout) == EOF)
        return 6;
    first_write = drain_available(descriptors[0]);

    if (fputc('\n', stdout) == EOF)
        return 7;
    newline_write = drain_available(descriptors[0]);

    if (fflush(stdout) != 0)
        return 8;
    explicit_flush = drain_available(descriptors[0]);

    dprintf(
        saved_stdout,
        "first=%d newline=%d flush=%d\n",
        first_write,
        newline_write,
        explicit_flush
    );

    close(saved_stdout);
    close(descriptors[0]);
    return 0;
}
