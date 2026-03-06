using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthCheckAI.Helpers
{
    public static class TwIdValidator
    {
        private static readonly Dictionary<char, int> LetterMapping = new()
        {
            ['A'] = 10,
            ['B'] = 11,
            ['C'] = 12,
            ['D'] = 13,
            ['E'] = 14,
            ['F'] = 15,
            ['G'] = 16,
            ['H'] = 17,
            ['I'] = 34,
            ['J'] = 18,
            ['K'] = 19,
            ['L'] = 20,
            ['M'] = 21,
            ['N'] = 22,
            ['O'] = 35,
            ['P'] = 23,
            ['Q'] = 24,
            ['R'] = 25,
            ['S'] = 26,
            ['T'] = 27,
            ['U'] = 28,
            ['V'] = 29,
            ['W'] = 32,
            ['X'] = 30,
            ['Y'] = 31,
            ['Z'] = 33
        };

        public static bool IsValidTaiwanId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            id = id.Trim().ToUpper();

            // 格式檢查：1英文字 + 9數字
            if (id.Length != 10)
                return false;

            if (!char.IsLetter(id[0]))
                return false;

            if (!LetterMapping.ContainsKey(id[0]))
                return false;

            if (id[1] != '1' && id[1] != '2')
                return false;

            if (!id.Substring(1).All(char.IsDigit))
                return false;

            int code = LetterMapping[id[0]];
            int x1 = code / 10;
            int x2 = code % 10;

            int sum = x1 * 1 + x2 * 9;

            int[] weights = { 8, 7, 6, 5, 4, 3, 2, 1, 1 };

            for (int i = 1; i < 10; i++)
            {
                sum += (id[i] - '0') * weights[i - 1];
            }

            return sum % 10 == 0;
        }
    }
}