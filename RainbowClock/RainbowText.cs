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

        public static string Apply(string input)
        {
            var sb = new StringBuilder(input.Length * 24);
            foreach (char c in input)
            {
                sb.Append("<color=").Append(Colors[_index]).Append('>').Append(c).Append("</color>");
                _index = (_index + 1) % Colors.Length;
            }

            int addValue = (Colors.Length - 1) - input.Length;
            if (input.Length < 10)
            {
                _index += addValue;
                if (_index > Colors.Length - 1)
                {
                    _index -= Colors.Length;
                }
            }
            return sb.ToString();
        }
    }
}
