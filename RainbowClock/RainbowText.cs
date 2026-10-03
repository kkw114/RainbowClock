using System.Text;

namespace RainbowClock
{
    /// <summary>
    /// 彩虹时钟：逐字符着色（颜色表来自 Quest 版 ClockMod）。
    /// </summary>
    public static class RainbowText
    {
        private static int _index = new System.Random().Next(12);

        private static readonly string[] Colors =
        {
            "#ff6060", "#ffa060", "#ffff60", "#a0ff60", "#60ff60", "#60ffa0",
            "#60ffff", "#60a0ff", "#6060ff", "#a060ff", "#ff60ff", "#ff60a0"
        };

        /// <summary>
        /// 逐字符套彩虹色。已是富文本标签的内容（如电量自带的 &lt;color=#A3CD20&gt;78%&lt;/color&gt;）
        /// 必须整段透传：标签若也被逐字符着色，'&lt;' 'c' 'o' ... 会被 TMP 当成可见文字显示出来，
        /// 界面上就会出现 "&lt;color=#A3CD20&gt;78%&lt;/color&gt;" 这样的字面标签。
        /// 标签不参与配色，颜色索引只在可见字符上前进，保持与原实现一致的取色节奏。
        /// </summary>
        public static string Apply(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input ?? "";
            }

            var sb = new StringBuilder(input.Length * 24);
            int visibleCount = 0;
            int i = 0;
            while (i < input.Length)
            {
                if (TryReadTag(input, i, out int tagEnd))
                {
                    // 富文本标签：原样保留，不占颜色位
                    sb.Append(input, i, tagEnd - i);
                    i = tagEnd;
                    continue;
                }

                sb.Append("<color=").Append(Colors[_index]).Append('>').Append(input[i]).Append("</color>");
                _index = (_index + 1) % Colors.Length;
                visibleCount++;
                i++;
            }

            int addValue = (Colors.Length - 1) - visibleCount;
            if (visibleCount < 10)
            {
                _index += addValue;
                if (_index > Colors.Length - 1)
                {
                    _index -= Colors.Length;
                }
            }
            return sb.ToString();
        }

        /// <summary>标签内部长度上限，超过则视为正文而非标签。</summary>
        private const int MaxTagLength = 64;

        /// <summary>
        /// 判断 <paramref name="start"/> 处是否为一个完整的富文本标签（形如 &lt;color=#RRGGBB&gt; 或 &lt;/color&gt;）。
        /// 只有在能定位到收尾 '&gt;'、内部不含 '&lt;' 且长度合理时才认定为标签，
        /// 避免把正文里落单的 '&lt;' 连同后面一大段文字误吞掉。
        /// </summary>
        private static bool TryReadTag(string input, int start, out int end)
        {
            end = start;
            if (input[start] != '<')
            {
                return false;
            }
            int close = input.IndexOf('>', start + 1);
            if (close < 0)
            {
                return false;
            }
            int inner = close - start - 1;
            if (inner <= 0 || inner > MaxTagLength)
            {
                return false;
            }
            // TMP 标签只有两种开头：起始标签 <color ...>（'<' 后是字母）
            // 与闭合标签 </color>（'<' 后是 '/'，再跟字母）。
            // 正文里落单的 '<' 后面通常不是这两种形式，据此避免误吞后面的文字。
            int head = start + 1;
            if (input[head] == '/')
            {
                head++;
            }
            if (head >= close)
            {
                return false;
            }
            char initial = input[head];
            bool letterHead = (initial >= 'a' && initial <= 'z') || (initial >= 'A' && initial <= 'Z');
            if (!letterHead)
            {
                return false;
            }
            int nextOpen = input.IndexOf('<', start + 1);
            if (nextOpen >= 0 && nextOpen < close)
            {
                return false;
            }
            end = close + 1;
            return true;
        }
    }
}
