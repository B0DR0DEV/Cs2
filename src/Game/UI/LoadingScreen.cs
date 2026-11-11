using Godot;

namespace StrikeZone.Game
{
    /// <summary>Full-screen loading card: map name, mode and a gameplay tip.</summary>
    public partial class LoadingScreen : CanvasLayer
    {
        private static readonly string[] Tips =
        {
            "Отпустите движение или нажмите противоположное направление перед выстрелом: стоя вы стреляете точно.",
            "Первые пули очереди летят вверх. Тяните прицел вниз, а после 9-го выстрела — в сторону.",
            "Шаг (слабое отклонение джойстика) бесшумен — противник не услышит ваших шагов.",
            "Деревянные двери и тонкий металл простреливаются, бетон — нет.",
            "Без шлема даже пистолет убивает одним попаданием в голову.",
            "Прыжок с приседом позволяет забраться на ящик, до которого не достать обычным прыжком."
        };

        private Label _title;
        private Label _mode;
        private Label _tip;

        public LoadingScreen()
        {
            Layer = 100;
        }

        public override void _Ready()
        {
            var root = Ui.Fill(new Control());
            AddChild(root);
            root.AddChild(Ui.Fill(new TextureRect
            {
                Texture = ProceduralTextures.Gradient("loading", new Color(0.09f, 0.11f, 0.14f), new Color(0.02f, 0.025f, 0.035f)),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale
            }));
            _mode = Ui.Place(Ui.Label("", 24, Ui.Accent, Ui.Wide), 0f, 1f, 1f, 1f, 80f, -300f, -80f, -260f);
            _title = Ui.Place(Ui.Label("", 96, Ui.Text, Ui.Wide), 0f, 1f, 1f, 1f, 76f, -260f, -80f, -150f);
            _tip = Ui.Place(Ui.Label("", 24, Ui.Muted), 0f, 1f, 1f, 1f, 80f, -130f, -80f, -70f);
            _tip.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            root.AddChild(_mode);
            root.AddChild(_title);
            root.AddChild(_tip);
            var bar = Ui.Place(Ui.Rect(Ui.Accent), 0f, 1f, 1f, 1f, 80f, -40f, -80f, -34f);
            root.AddChild(bar);
        }

        public void Show(string title, string mode)
        {
            if (_title == null) return;
            _title.Text = title;
            _mode.Text = mode;
            _tip.Text = "СОВЕТ: " + Tips[(int)(Time.GetTicksMsec() % (ulong)Tips.Length)];
        }
    }
}
