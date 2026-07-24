using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthCheckAI.Helpers
{
    public static class TwIdValidator
    {
        public static bool IsValid(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            // 1. 去除前後空白並強制轉大寫
            id = id.Trim().ToUpper();

            // 2. 使用正規表達式檢查基本格式 (1碼大寫英文 + 第二碼1或2 + 8碼數字)
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"^[A-Z][12]\d{8}$"))
            {
                return false;
            }

            // 3. 定義 A~Z 對應的代號數值 (已修正 I, O, W, X, Y, Z 的特殊對應)
            // A=10, B=11, C=12, D=13, E=14, F=15, G=16, H=17, J=18, K=19...
            int[] letterValues = {
        10, 11, 12, 13, 14, 15, 16, 17, 34, 18, // A~J
        19, 20, 21, 22, 35, 23, 24, 25, 26, 27, // K~T
        28, 29, 32, 30, 31, 33                  // U~Z
    };

            char firstChar = id[0];
            int letterNum = letterValues[firstChar - 'A'];

            // 4. 拆解首字母轉換後的十位數 (n1) 與個位數 (n2)
            int n1 = letterNum / 10;
            int n2 = letterNum % 10;

            // 5. 計算首碼加權值
            int sum = n1 * 1 + n2 * 9;

            // 6. 計算中間 8 位流水號的加權值 (權重依序為 8, 7, 6, 5, 4, 3, 2, 1)
            int[] weights = { 8, 7, 6, 5, 4, 3, 2, 1 };
            for (int i = 0; i < 8; i++)
            {
                sum += (id[i + 1] - '0') * weights[i];
            }

            // 7. 加上最後一碼檢查碼 (權重為 1)
            sum += (id[9] - '0') * 1;

            // 8. 總和必須能被 10 整除才算有效！
            return sum % 10 == 0;
        }
    }
}