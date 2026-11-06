#include <stdint.h>
#include <stdio.h>

int FuzzerInitialize(int *argc, char ***argv);
void FuzzerCleanup(void);
int FuzzerTestOneInput(const uint8_t *data, size_t size);

int main(void)
{
    int argc = 1;
    char *args[] = {"fuzzer-return-probe", NULL};
    char **argv = args;
    uint8_t input[16] = {0};

    if (!FuzzerInitialize(&argc, &argv))
        return 2;

    int result = FuzzerTestOneInput(input, sizeof(input));
    printf("callback_return=%d\n", result);
    FuzzerCleanup();
    return 0;
}
