using UnityEngine;
using UnityEngine.UIElements;

namespace Nexus.UI.Effects
{
    public class CircleGlow : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<CircleGlow, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private UxmlColorAttributeDescription glowColor = new UxmlColorAttributeDescription { name = "glow-color", defaultValue = Color.white };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var element = (CircleGlow)ve;
                element.GlowColor = glowColor.GetValueFromBag(bag, cc);
            }
        }

        private Color _glowColor = Color.white;
        public Color GlowColor
        {
            get => _glowColor;
            set { _glowColor = value; MarkDirtyRepaint(); }
        }

        public CircleGlow()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 1f || r.height < 1f) return;

            var w = r.width;
            var h = r.height;
            var center = new Vector3(w * 0.5f, h * 0.5f, Vertex.nearZ);
            var radius = Mathf.Min(w, h) * 0.5f * 0.8f;

            var mesh = ctx.Allocate(42, 120);


            var centerColor = GlowColor;
            centerColor.a *= 0.2f;
            mesh.SetNextVertex(new Vertex { position = center, tint = centerColor });


            int segments = 40;
            var edgeColor = GlowColor;
            edgeColor.a = 0;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = center.x + Mathf.Cos(angle) * radius;
                float y = center.y + Mathf.Sin(angle) * radius;

                mesh.SetNextVertex(new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = edgeColor });
            }




            for (int i = 0; i < segments; i++)
            {
                mesh.SetNextIndex(0);
                mesh.SetNextIndex((ushort)(i + 1));
                mesh.SetNextIndex((ushort)(i + 2));
            }
        }
    }
}
