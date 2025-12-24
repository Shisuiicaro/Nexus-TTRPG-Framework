using UnityEngine;
using UnityEngine.UIElements;

namespace Nexus.UI.Custom
{
    public class ChamferedBox : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ChamferedBox, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private UxmlFloatAttributeDescription chamferSize = new UxmlFloatAttributeDescription { name = "chamfer-size", defaultValue = 10f };
            private UxmlColorAttributeDescription fillColor = new UxmlColorAttributeDescription { name = "fill-color", defaultValue = new Color(1, 1, 1, 0.03f) };
            private UxmlColorAttributeDescription borderColor = new UxmlColorAttributeDescription { name = "border-color", defaultValue = new Color(1, 1, 1, 0.1f) };
            private UxmlFloatAttributeDescription borderWidth = new UxmlFloatAttributeDescription { name = "border-width", defaultValue = 1f };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var element = (ChamferedBox)ve;
                element.ChamferSize = chamferSize.GetValueFromBag(bag, cc);
                element.FillColor = fillColor.GetValueFromBag(bag, cc);
                element.BorderColor = borderColor.GetValueFromBag(bag, cc);
                element.BorderWidth = borderWidth.GetValueFromBag(bag, cc);
            }
        }

        private float _chamferSize = 10f;
        public float ChamferSize
        {
            get => _chamferSize;
            set { _chamferSize = value; MarkDirtyRepaint(); }
        }

        private Color _fillColor = new Color(1, 1, 1, 0.03f);
        public Color FillColor
        {
            get => _fillColor;
            set { _fillColor = value; MarkDirtyRepaint(); }
        }

        private Color _borderColor = new Color(1, 1, 1, 0.1f);
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; MarkDirtyRepaint(); }
        }

        private float _borderWidth = 1f;
        public float BorderWidth
        {
            get => _borderWidth;
            set { _borderWidth = value; MarkDirtyRepaint(); }
        }

        private CustomStyleProperty<Color> _fillColorProperty = new CustomStyleProperty<Color>("--fill-color");
        private CustomStyleProperty<Color> _borderColorProperty = new CustomStyleProperty<Color>("--border-color");
        private CustomStyleProperty<Color> _borderTopColorProperty = new CustomStyleProperty<Color>("--border-top-color");
        private CustomStyleProperty<Color> _borderRightColorProperty = new CustomStyleProperty<Color>("--border-right-color");

        public ChamferedBox()
        {
            style.backgroundColor = Color.clear;
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<CustomStyleResolvedEvent>(evt => MarkDirtyRepaint());
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 1f || r.height < 1f) return;

            var paint2D = ctx.painter2D;
            
            var w = r.width;
            var h = r.height;
            var c = ChamferSize;


            Color bgColor = new Color(1, 1, 1, 0.03f);
            if (customStyle.TryGetValue(_fillColorProperty, out var customColor))
            {
                bgColor = customColor;
            }


            if (bgColor.a > 0.001f)
            {
                paint2D.fillColor = bgColor;
                paint2D.BeginPath();
                paint2D.MoveTo(new Vector2(c, 0));           
                paint2D.LineTo(new Vector2(w, 0));           
                paint2D.LineTo(new Vector2(w, h - c));       
                paint2D.LineTo(new Vector2(w - c, h));       
                paint2D.LineTo(new Vector2(0, h));           
                paint2D.LineTo(new Vector2(0, c));           
                paint2D.ClosePath();
                paint2D.Fill();
            }


            Color borderColor = Color.clear;
            if (customStyle.TryGetValue(_borderColorProperty, out var customBorderColor))
            {
                borderColor = customBorderColor;
            }
            else
            {
                 borderColor = resolvedStyle.borderTopColor;
            }

            Color borderTopColor = borderColor;
            if (customStyle.TryGetValue(_borderTopColorProperty, out var customTopColor))
            {
                borderTopColor = customTopColor;
            }

            Color borderRightColor = borderColor;
            if (customStyle.TryGetValue(_borderRightColorProperty, out var customRightColor))
            {
                borderRightColor = customRightColor;
            }
            
            if (BorderWidth > 0)
            {
                float half = BorderWidth * 0.5f;
                paint2D.lineWidth = BorderWidth;
                

                if (borderTopColor.a > 0.001f)
                {
                    paint2D.strokeColor = borderTopColor;
                    paint2D.BeginPath();
                    paint2D.MoveTo(new Vector2(c, half)); 
                    paint2D.LineTo(new Vector2(w - half, half));
                    paint2D.Stroke();
                }


                if (borderRightColor.a > 0.001f)
                {
                    paint2D.strokeColor = borderRightColor;
                    paint2D.BeginPath();
                    paint2D.MoveTo(new Vector2(w - half, half));
                    paint2D.LineTo(new Vector2(w - half, h - c));
                    paint2D.Stroke();
                }


                if (borderColor.a > 0.001f)
                {
                    paint2D.strokeColor = borderColor;
                    paint2D.BeginPath();
                    

                    paint2D.MoveTo(new Vector2(w - c, h - half));
                    paint2D.LineTo(new Vector2(half, h - half));
                    

                    paint2D.LineTo(new Vector2(half, c));
                    
                    paint2D.Stroke();
                }
            }
        }
    }
}
