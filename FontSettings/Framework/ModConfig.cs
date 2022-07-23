﻿namespace FontSettings.Framework
{
    internal class ModConfig
    {
        public string ExampleText { get; set; } = "AaBbYyZz\n测试用例";

        internal FontConfigs Fonts { get; set; }  // 这个不要储存在config.json里，故设为internal。

        public int MinCharOffsetX { get; set; } = -10;

        public int MaxCharOffsetX { get; set; } = 10;

        public int MinCharOffsetY { get; set; } = -10;

        public int MaxCharOffsetY { get; set; } = 10;
    }
}
