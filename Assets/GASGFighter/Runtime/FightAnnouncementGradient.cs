using UnityEngine;
using UnityEngine.UI;

namespace GASG.Fighting
{
    // 文字のメッシュだけを着色し、画像素材がなくても銀色の縦グラデーションを描画する。
    [AddComponentMenu("")]
    public sealed class FightAnnouncementGradient : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || vertices.currentVertCount == 0) return;
            UIVertex vertex = default;
            float bottom = float.MaxValue;
            float top = float.MinValue;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                bottom = Mathf.Min(bottom, vertex.position.y);
                top = Mathf.Max(top, vertex.position.y);
            }
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                float height = Mathf.InverseLerp(bottom, top, vertex.position.y);
                Color silver = Color.Lerp(new Color(0.56f, 0.6f, 0.72f), Color.white, height);
                vertex.color = (Color)vertex.color * silver;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
