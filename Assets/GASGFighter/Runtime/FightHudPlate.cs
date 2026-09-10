using UnityEngine;
using UnityEngine.UI;

namespace GASG.Fighting
{
    // テクスチャを増やさず、斜めの端と上下の明暗でゲージを描画する。
    public sealed class FightHudPlate : Image
    {
        public bool mirror;
        public float slant = 8f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float cut = Mathf.Min(slant, r.width * 0.25f);
            float bottom = mirror ? cut : 0f;
            float top = mirror ? 0f : cut;
            // 質感や光沢は加えず、同じ色相の上下差だけでグラデーションを作る。
            Color lower = Color.Lerp(color, Color.black, 0.18f);
            Color upper = Color.Lerp(color, Color.white, 0.10f);
            vh.AddVert(new Vector3(r.xMin + bottom, r.yMin), lower, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin + top, r.yMax), upper, Vector2.up);
            vh.AddVert(new Vector3(r.xMax - bottom, r.yMax), upper, Vector2.one);
            vh.AddVert(new Vector3(r.xMax - top, r.yMin), lower, Vector2.right);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
