using UnityEngine;

namespace GASG.Fighting
{
    // 表示専用の固定長履歴。同一入力は行を増やさず継続フレームを加算する。
    public sealed class FightDisplayHistory
    {
        public const int Capacity = 32;
        public const int MaxDisplayFrames = 99;
        public struct Entry
        {
            public int Direction;
            public int Buttons;
            public int Frames;
        }

        private readonly Entry[] entries = new Entry[Capacity];
        private int newest = -1;
        public int Count { get; private set; }

        public Entry GetNewest(int index) => index >= 0 && index < Count
            ? entries[(newest - index + Capacity) % Capacity] : default;

        public void Advance(Vector2 move, int buttons)
        {
            int x = move.x > 0.45f ? 1 : move.x < -0.45f ? -1 : 0;
            int y = move.y > 0.45f ? 1 : move.y < -0.45f ? -1 : 0;
            int direction = 5 + x + y * 3;

            // 攻撃ボタンだけのフレームは、ニュートラルではなく方向表示なしにする。
            // 0はFighterInputDirectionの有効値ではないため、HUDでは方向アイコンを隠す。
            if (direction == 5 && buttons != 0)
            {
                direction = 0;
            }
            if (Count > 0 && entries[newest].Direction == direction && entries[newest].Buttons == buttons)
            {
                if (entries[newest].Frames < MaxDisplayFrames) entries[newest].Frames++;
                return;
            }
            newest = (newest + 1) % Capacity;
            entries[newest] = new Entry { Direction = direction, Buttons = buttons, Frames = 1 };
            Count = Mathf.Min(Count + 1, Capacity);
        }

        public void Clear()
        {
            Count = 0;
            newest = -1;
        }
    }
}
