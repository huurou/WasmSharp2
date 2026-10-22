#include <stdint.h>

// 入力2行列と出力は重ならない32×32のrow-major領域を指定する。
void matrix(const uint32_t *left, const uint32_t *right, uint32_t *output)
{
    for (uint32_t row = 0; row < 32; row++)
    {
        for (uint32_t column = 0; column < 32; column++)
        {
            uint32_t sum = 0;
            for (uint32_t inner = 0; inner < 32; inner++)
            {
                // unsignedの積和でmod2^32を保ち、前回の出力を読み込まない。
                sum += left[row * 32 + inner] * right[inner * 32 + column];
            }
            output[row * 32 + column] = sum;
        }
    }
}
